using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    sealed partial class PocoTypeSystem
    {
        /// <summary>
        /// To efficiently exclude types we use an array of reference counter for each (non nullable) types.
        /// </summary>
        sealed class Excluder
        {
            // We can work with a simple array of integer:
            // 0 for excluded, >= 1 for included.
            // - For back ref aware types that can live with a variable number of types (IUnionPocoType for their AllowedTypes
            //   and ICompositePocoType for their field's types), this is the count of included back reference.
            // - ICollectionPocoType (for their ItemTypes) and IAbstractPocoType (for their generic arguments) are back ref aware types that
            //   require all their types.
            //   - For ICollectionPocoType this is initialized to 1 (like back ref unaware types): as soon as a referenced type is
            //     excluded, the owner type is excluded.
            //   - For IAbstractPocoType we track the number of primaries (and the exclusion of the last back reference excludes the abstract poco type).
            readonly int[] _backRefCount;
            readonly PocoTypeRawSet _source;
            IPocoType? _iPoco;

            // If we need it, it exists in the TypeSystem.
            IPocoType Poco => _iPoco ??= _source.TypeSystem.FindByType( typeof( IPoco ) )!;

            internal Excluder( PocoTypeRawSet source, bool allowEmptyRecord, bool allowEmptyPoco, Func<IPocoType, bool> lowLevelFilter )
            {
                _source = source;
                _backRefCount = new int[source.TypeSystem.AllNonNullableTypes.Count];
                List<IPocoType>? toBeExcluded = null;
                foreach( var t in source )
                {
                    // Applies the low level filter first.
                    if( !lowLevelFilter( t.NonNullable ) )
                    {
                        toBeExcluded ??= new List<IPocoType>();
                        toBeExcluded.Add( t );
                        // Set to 1 otherwise DoExclude will early exit.
                        _backRefCount[t.Index >> 1] = 1;
                    }
                    else
                    {
                        InitialHandleType( source, t, _backRefCount, allowEmptyRecord, allowEmptyPoco, ref toBeExcluded );
                    }
                }
                if( toBeExcluded != null )
                {
                    foreach( var c in toBeExcluded )
                    {
                        DoExclude( c, false );
                    }
                }

                static void InitialHandleType( PocoTypeRawSet source,
                                               IPocoType t,
                                               int[] backRefCount,
                                               bool allowEmptyRecord,
                                               bool allowEmptyPoco,
                                               ref List<IPocoType>? toBeExcluded )
                {
                    switch( t )
                    {
                        case ICompositePocoType composite:
                            {
                                // For PocoFields, whether AbstractReadOnly fields' types have been included or not
                                // is transparent here: if such field's type has been included, then if the type
                                // is excluded, its occurence will be decremented just like for regular fields.
                                // There may remain a single AbstractReadOnly field in a Poco and this is fine
                                // since it has been included.

                                // Instead of handling the owner's type in DoExclude based on allowEmptyRecord/Poco,
                                // we simply artificially boost the back reference count here.

                                // Allower may have allowed an empty anonymous record. This is invalid (an empty value tuple
                                // is basically invalid). Rule 12.
                                bool allowEmpty = composite.Kind == PocoTypeKind.AnonymousRecord
                                                    ? false
                                                    : composite.Kind == PocoTypeKind.Record
                                                        ? allowEmptyRecord
                                                        : allowEmptyPoco;

                                var fieldCount = allowEmpty ? int.MaxValue : composite.Fields.Count( f => source.Contains( f.Type ) );
                                // If fieldCount = 0, we are coming from a source that allowed the empties and we no more allow them. 
                                if( fieldCount == 0 )
                                {
                                    toBeExcluded ??= new List<IPocoType>();
                                    toBeExcluded.Add( composite );
                                    // Set to 1 otherwise DoExclude will early exit.
                                    fieldCount = 1;
                                }
                                backRefCount[t.Index >> 1] = fieldCount;
                                break;
                            }
                        case IUnionPocoType u:
                            // Allower ensures these.
                            Throw.DebugAssert( "At least one type in the union is included.", u.AllowedTypes.Any( source.Contains ) );
                            backRefCount[t.Index >> 1] = u.AllowedTypes.Count( source.Contains );
                            break;
                        case IAbstractPocoType abs:
                            {
                                // Allower ensures these.
                                Throw.DebugAssert( "Generic AbstractPoco require all their GenericArguments.", t is not IAbstractPocoType a || a.GenericArguments.All( a => source.Contains( a.Type ) ) );
                                // The count is the number of primaries: back references are for generic arguments and we don't need
                                // to track them (as soon as a back reference is excluded, the abstract is excluded).
                                int primaryCount = abs.PrimaryPocoTypes.Count();
                                // Allower may have included an abstract without implementations.
                                // Fix this now (Rule 7).
                                if( primaryCount == 0 )
                                {
                                    toBeExcluded ??= new List<IPocoType>();
                                    toBeExcluded.Add( abs );
                                    primaryCount = 1;
                                }
                                backRefCount[t.Index >> 1] = primaryCount;
                                break;
                            }
                        default:
                            Throw.DebugAssert( t.Kind == PocoTypeKind.Enum
                                               || t is ICollectionPocoType
                                               || t.Kind == PocoTypeKind.Basic
                                               || t.Kind == PocoTypeKind.Any );

                            // Allower ensures these.
                            Throw.DebugAssert( "Enum has only one backref: its underlying type.", t is not IEnumPocoType e || source.Contains( e.UnderlyingType ) );
                            Throw.DebugAssert( "Collections require all their ItemTypes.", t is not ICollectionPocoType c || c.ItemTypes.All( source.Contains ) );
                            //  
                            backRefCount[t.Index >> 1] = 1;
                            break;
                    }
                }

            }

            internal void DoExclude( IPocoType t, bool isRoot )
            {
                ref var count = ref _backRefCount[t.Index >> 1];
                if( count == 0 ) return;
                _source.Remove( t );
                // Prevent reentrancy.
                count = 0;
                // Handle back references.
                var b = t.FirstBackReference;
                while( b != null )
                {
                    // b.Index == -1: Oblivious to non oblivious Owner reference: we exclude the Owner.
                    // b.Owner is IAbstractPocoType: Owner is a IAbstractPocoType, its backRefCount is its number of
                    //                               primaries. Back reference tracks its generic arguments and the excluded
                    //                               type is a generic argument: exclude it.
                    if( b.Index == -1 || b.Owner is IAbstractPocoType )
                    {
                        DoExclude( b.Owner, false );
                    }
                    else
                    {
                        // Common back reference management: decrement and check the 0.
                        DecrementRefCount( b.Owner );
                    }
                    b = b.NextRef;
                }
                // Handles Collection AbstractReadOnly => MutableCollection.
                if( t is ICollectionPocoType c && !c.IsAbstractReadOnly && c.AbstractReadOnlyCollection != null )
                {
                    DoExclude( c.AbstractReadOnlyCollection, false );
                }
                else
                {
                    // Handles Poco types: the Primary is the key.
                    // Primary and Secondary are "unified".
                    // Abstract impacts their primaries only if they are explicitly excluded (isRoot is true).
                    if( t is IAbstractPocoType a )
                    {
                        if( isRoot )
                        {
                            foreach( var p in a.PrimaryPocoTypes )
                            {
                                DoExclude( p, false );
                            }
                        }
                    }
                    else if( t is ISecondaryPocoType secondary )
                    {
                        DoExclude( secondary.PrimaryPocoType, false );
                    }
                    else if( t is IPrimaryPocoType primary )
                    {
                        foreach( var s in primary.SecondaryTypes )
                        {
                            DoExclude( s, false );
                        }
                        foreach( var abs in primary.AbstractTypes )
                        {
                            DecrementRefCount( abs );
                        }
                        // Handle the Poco that doesn't appear in the AbstractTypes.
                        DecrementRefCount( Poco );
                    }
                }
            }

            void DecrementRefCount( IPocoType t )
            {
                ref var c = ref _backRefCount[t.Index >> 1];
                if( c == 1 ) DoExclude( t, false );
                else --c;
            }
        }
    }
}
