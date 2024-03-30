using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystemBuilder
    {
        IPocoType? RegisterListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx, bool isConcrete )
        {
            var listOrHashSet = isList ? "List" : "HashSet";
            var tI = RegisterItemType( monitor, nType, ctx, isConcrete, listOrHashSet );
            if( tI == null ) return null;
            if( !isList && !CheckHashSetItemType( monitor, nType, ctx, tI ) )
            {
                return null;
            }
            // The RegularCollection came last in the battle: instead of integrating it in the
            // existing RegisterListOrSetCore we handle it here. The good news is that it simplifies the code.
            Throw.DebugAssert( "Only abstract read only collections can have a null regular and a read only collection cannot be a collection item",
                               tI.RegularType != null );

            IPocoType tIRegular = tI.RegularType;
            ICollectionPocoType? regularCollection = null;
            var nTypeConcrete = isConcrete ? nType : nType.SetReferenceTypeDefinition( isList ? typeof( List<> ) : typeof( HashSet<> ) );
            if( tI != tIRegular )
            {
                // nTypeConcrete may be nullable. Take the non nullable.
                regularCollection = Unsafe.As<ICollectionPocoType>( RegisterConcreteListOrSet( isList, nTypeConcrete, listOrHashSet, tIRegular, null ).NonNullable );
            }
            var concreteType = RegisterConcreteListOrSet( isList, nTypeConcrete, listOrHashSet, tI, regularCollection );
            return isConcrete ? concreteType : RegisterAbstractListOrSet( isList, nType, listOrHashSet, tI, concreteType.NonNullable );
        }

        IPocoType? RegisterItemType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, bool isConcrete, string listOrHashSet )
        {
            bool valid = ctx.EnterListSetOrDictionary( monitor, nType, isConcrete, listOrHashSet );
            var tI = Register( monitor, ctx, nType.GenericTypeArguments[0] );
            return valid ? tI : null;
        }

        static bool CheckHashSetItemType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, IPocoType tI )
        {
            if( !tI.IsReadOnlyCompliant )
            {
                monitor.Error( $"{ctx}: '{nType.Type:C}' item type cannot be '{tI.CSharpName}' because this type is not read-only compliant." );
                return false;
            }
            return true;
        }

        IPocoType? RegisterReadOnlyListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx )
        {
            var listOrHashSet = isList ? "List" : "HashSet";
            var tI = RegisterItemType( monitor, nType, ctx, false, listOrHashSet );
            if( tI == null ) return null;
            // IReadOnlySet<object> is allowed.
            if( !isList && tI.Kind != PocoTypeKind.Any && !CheckHashSetItemType( monitor, nType, ctx, tI ) )
            {
                return null;
            }
            var csharpName = $"IReadOnly{(isList ? "List" : "Set")}<{tI.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                IPocoType? obliviousType = null;
                if( !tI.IsOblivious )
                {
                    Throw.DebugAssert( tI.ObliviousType.Type == tI.Type );
                    var tOblivious = (isList ? typeof( IReadOnlyList<> ) : typeof( IReadOnlySet<> ))
                                        .MakeGenericType( tI.Type );
                    if( _typeCache.TryGetValue( tOblivious, out obliviousType ) )
                    {
                        var obliviousTypeName = $"IReadOnly{(isList ? "List" : "Set")}<{tI.ObliviousType.CSharpName}>";
                        obliviousType = PocoType.CreateAbstractCollection( this,
                                                                           tOblivious,
                                                                           obliviousTypeName,
                                                                           isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                           new[] { tI.ObliviousType },
                                                                           null ).ObliviousType;
                        _typeCache.Add( tOblivious, obliviousType );
                        _typeCache.Add( obliviousTypeName, obliviousType.NonNullable );
                    }
                }
                result = PocoType.CreateAbstractCollection( this,
                                                            nType.Type,
                                                            csharpName,
                                                            isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                            new[] { tI },
                                                            obliviousType );
                Throw.DebugAssert( !result.IsNullable );
                if( obliviousType == null )
                {
                    _typeCache.Add( result.Type, result.Nullable );
                }
                _typeCache.Add( csharpName, result );
            }
            Throw.DebugAssert( !result.IsNullable );
            return nType.IsNullable ? result.Nullable : result;
        }

        IPocoType RegisterConcreteListOrSet( bool isList,
                                             IExtNullabilityInfo nType,
                                             string listOrHashSet,
                                             IPocoType tI,
                                             ICollectionPocoType? regularCollection )
        {
            var csharpName = $"{listOrHashSet}<{tI.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                Type t = nType.Type;
                var obliviousItemType = tI.ObliviousType;
                if( !_typeCache.TryGetValue( t, out var obliviousType ) )
                {
                    // The concrete oblivious is Oblivious and Final.
                    var oName = $"{listOrHashSet}<{obliviousItemType.CSharpName}>";
                    // If the regular collection is available and its item type happens to be oblivious then
                    // it is the regular collection.
                    var obliviousRegular = regularCollection?.ItemTypes[0] == obliviousItemType ? regularCollection : null;

                    Throw.DebugAssert( "The oblivious item types are necessarily compliant with the regular collection: " +
                                       "oblivious => regular (or null regular for abstract read only but we are not in this case).",
                                       obliviousItemType.IsRegular );

                    obliviousType = PocoType.CreateCollection( this,
                                                               t,
                                                               oName,
                                                               oName,
                                                               isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                               itemType: obliviousItemType,
                                                               obliviousType: null,
                                                               finalType: null,
                                                               obliviousRegular ).ObliviousType;
                    _typeCache.Add( t, obliviousType );
                    _typeCache.Add( oName, obliviousType.NonNullable );
                }
                Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.IsNullable );
                // We are the oblivious if the item is oblivious (whatever isRegular is).
                if( tI.IsOblivious )
                {
                    result = obliviousType;
                }
                else
                {
                    // If the regular collection is available, it is the one.
                    // Otherwise, this is necessarily its own RegularCollection.
                    result = PocoType.CreateCollection( this,
                                                        t,
                                                        csharpName,
                                                        obliviousType.ImplTypeName,
                                                        obliviousType.Kind,
                                                        tI,
                                                        obliviousType,
                                                        obliviousType.StructuralFinalType,
                                                        regularCollection );
                    Throw.DebugAssert( !result.IsNullable );
                    _typeCache.Add( csharpName, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result.NonNullable;
        }

        IPocoType RegisterAbstractListOrSet( bool isList,
                                             IExtNullabilityInfo nType,
                                             string listOrHashSet,
                                             IPocoType tI,
                                             IPocoType concreteCollection )
        {
            var csharpName = $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                Type t = nType.Type;
                // Type erasure of SecondaryPoco to PrimaryPoco for abstract collection only.
                var obliviousItemType = tI.ObliviousType;
                if( obliviousItemType.Kind == PocoTypeKind.SecondaryPoco )
                {
                    obliviousItemType = Unsafe.As<ISecondaryPocoType>( obliviousItemType ).PrimaryPocoType;
                    Throw.DebugAssert( obliviousItemType.IsOblivious );
                    t = (isList ? typeof( IList<> ) : typeof( ISet<> )).MakeGenericType( obliviousItemType.Type );
                }
                if( !_typeCache.TryGetValue( t, out var obliviousType ) )
                {
                    var oName = $"{(isList ? "IList" : "ISet")}<{obliviousItemType.CSharpName}>";
                    var oTypeName = GetAbstractionImplTypeSupport( isList, listOrHashSet, obliviousItemType );
                    // If an adapter is required, this is the final type, otherwise it is the concrete's one.
                    IPocoType? finalType = null;
                    if( oTypeName == null )
                    {
                        oTypeName = concreteCollection.ImplTypeName;
                        finalType = concreteCollection.FinalType;
                    }
                    obliviousType = PocoType.CreateCollection( this,
                                                               t,
                                                               oName,
                                                               oTypeName,
                                                               isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                               itemType: obliviousItemType,
                                                               obliviousType: null,
                                                               finalType,
                                                               concreteCollection.RegularType ).ObliviousType;
                    _typeCache.Add( t, obliviousType );
                    _typeCache.Add( oName, obliviousType.NonNullable );
                }
                Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.IsNullable );
                // We are the oblivious if the item is oblivious.
                if( tI.IsOblivious )
                {
                    result = obliviousType;
                }
                else
                {
                    result = PocoType.CreateCollection( this,
                                                        t,
                                                        csharpName,
                                                        obliviousType.ImplTypeName,
                                                        obliviousType.Kind,
                                                        tI,
                                                        obliviousType,
                                                        obliviousType.StructuralFinalType,
                                                        concreteCollection.RegularType );
                    Throw.DebugAssert( !result.IsNullable );
                    _typeCache.Add( csharpName, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result.NonNullable;

            string? GetAbstractionImplTypeSupport( bool isList, string listOrHashSet, IPocoType tI )
            {
                string? typeName = null;
                if( tI.Type.IsValueType )
                {
                    Throw.DebugAssert( "Value types are implemented by themselves.", tI.CSharpName == tI.ImplTypeName );
                    // For value type item, use our covariant implementations.
                    // We use the Oblivious type name as a minor optimization for Roslyn here when the item
                    // is an anonymous record: instead of using the CSharName with its field names that will
                    // create useless TupleNamesAttribute, the oblivious has no field names.
                    if( tI.IsNullable )
                    {
                        Throw.DebugAssert( typeof( CovariantHelpers.CovNullableValueList<> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                           == "CK.Core.CovariantHelpers.CovNullableValueList<>" );
                        Throw.DebugAssert( typeof( CovariantHelpers.CovNullableValueHashSet<> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                           == "CK.Core.CovariantHelpers.CovNullableValueHashSet<>" );

                        typeName = $"CovariantHelpers.CovNullableValue{listOrHashSet}<{tI.NonNullable.ObliviousType.ImplTypeName}>";
                    }
                    else
                    {
                        Throw.DebugAssert( typeof( CovariantHelpers.CovNotNullValueList<> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                           == "CK.Core.CovariantHelpers.CovNotNullValueList<>" );
                        Throw.DebugAssert( typeof( CovariantHelpers.CovNotNullValueHashSet<> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                           == "CK.Core.CovariantHelpers.CovNotNullValueHashSet<>" );

                        typeName = $"CovariantHelpers.CovNotNullValue{listOrHashSet}<{tI.ObliviousType.ImplTypeName}>";
                    }
                }
                else
                {
                    Throw.DebugAssert( "We are on the oblivious item...", tI.IsOblivious && tI.IsNullable );
                    Throw.DebugAssert( "...and we erased the Secondary (to its primary).", tI.Kind != PocoTypeKind.SecondaryPoco );
                    if( tI.Kind == PocoTypeKind.PrimaryPoco )
                    {
                        Throw.DebugAssert( "Set cannot have a IPoco item (IPoco is not hash safe).", isList );
                        // For IPoco, use generated covariant implementations only if needed: if more than one Poco interface exist in the family.
                        // When the family contains only one interface (the primary one and no secondary), the standard List<PrimaryInterface> is fine.
                        if( Unsafe.As<IPrimaryPocoType>( tI ).FamilyInfo.Interfaces.Count > 1 )
                        {
                            typeName = EnsurePocoListOrHashSetType( Unsafe.As<IPrimaryPocoType>( tI ), isList, listOrHashSet );
                        }
                    }
                    else
                    {
                        // HashSet<> is not natively covariant. We support it here for
                        // string and other basic reference types.
                        if( !isList && tI.Kind != PocoTypeKind.Any )
                        {
                            Throw.DebugAssert( "Set cannot have a IPoco item (IPoco is not hash safe).", tI.Kind is not PocoTypeKind.PrimaryPoco and not PocoTypeKind.AbstractPoco );
                            typeName = EnsurePocoHashSetOfAbstractOrBasicRefType( tI );
                        }
                    }
                }
                return typeName;
            }
        }


        string EnsurePocoListOrHashSetType( IPrimaryPocoType tI, bool isList, string listOrHasSet )
        {
            Debug.Assert( tI.IsNullable );
            var genTypeName = $"Poco{listOrHasSet}_{tI.Index}_CK";
            if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
            {
                g = new PocoListOrHashSetRequiredSupport( tI, genTypeName, isList );
                _requiredSupportTypes.Add( genTypeName, g );
            }
            return g.FullName;
        }

        string EnsurePocoHashSetOfAbstractOrBasicRefType( IPocoType tI )
        {
            Debug.Assert( tI.IsNullable );
            var genTypeName = $"PocoHashSet_{tI.Index}_CK";
            if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
            {
                g = new PocoHashSetOfAbstractOrBasicRefRequiredSupport( tI, genTypeName );
                _requiredSupportTypes.Add( genTypeName, g );
            }
            return g.FullName;
        }
    }

}
