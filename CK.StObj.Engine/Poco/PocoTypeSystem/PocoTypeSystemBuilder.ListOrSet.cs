using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystemBuilder
    {
        IPocoType? RegisterListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx, bool isRegular )
        {
            var listOrHashSet = isList ? "List" : "HashSet";
            var tI = RegisterItemType( monitor, nType, ctx, isRegular, listOrHashSet );
            if( tI == null ) return null;
            if( !isList && !CheckHashSetItemType( monitor, nType, ctx, tI ) )
            {
                return null;
            }
            // The RegularCollection came last in the battle: instead of integrating it in the
            // existing RegisterListOrSetCore we handle it here. The good news is that it simplifies the code.
            ICollectionPocoType? regularCollection = null;
            IPocoType tIRegular = tI is IAnonymousRecordPocoType a ? a.UnnamedRecord : tI;
            if( !isRegular || tI != tIRegular )
            {
                var nRegular = isRegular
                                ? nType
                                : nType.SetReferenceTypeDefinition( isList ? typeof( List<> ) : typeof( HashSet<> ) );
                regularCollection = Unsafe.As<ICollectionPocoType>( RegisterListOrSetCore( isList, nRegular.ToNonNullable(), true, listOrHashSet, tIRegular, null ) );
            }
            return RegisterListOrSetCore( isList, nType, isRegular, listOrHashSet, tI, regularCollection );
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

        IPocoType RegisterListOrSetCore( bool isList,
                                         IExtNullabilityInfo nType,
                                         bool isRegular,
                                         string listOrHashSet,
                                         IPocoType tI,
                                         ICollectionPocoType? regularCollection )
        {
            var csharpName = isRegular
                                ? $"{listOrHashSet}<{tI.CSharpName}>"
                                : $"{(isList ? "IList" : "ISet")}<{tI.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                Type t = nType.Type;
                // Type erasure of SecondaryPoco to PrimaryPoco for abstract collection only.
                var obliviousItemType = tI.ObliviousType;
                if( !isRegular && obliviousItemType.Kind == PocoTypeKind.SecondaryPoco )
                {
                    obliviousItemType = Unsafe.As<ISecondaryPocoType>( obliviousItemType ).PrimaryPocoType;
                    Throw.DebugAssert( obliviousItemType.IsOblivious );
                    t = (isList ? typeof( IList<> ) : typeof( ISet<> )).MakeGenericType( obliviousItemType.Type );
                }
                if( !_typeCache.TryGetValue( t, out var obliviousType ) )
                {
                    IPocoType? finalType = null;
                    string oName, oTypeName;
                    if( isRegular )
                    {
                        // The regular is Oblivious and Final.
                        oName = $"{listOrHashSet}<{obliviousItemType.CSharpName}>";
                        oTypeName = oName;
                    }
                    else
                    {
                        Throw.DebugAssert( "IList or ISet: the regular collection has been created.", regularCollection != null );
                        oName = $"{(isList ? "IList" : "ISet")}<{obliviousItemType.CSharpName}>";
                        oTypeName = GetAbstractionImplTypeSupport( isList, listOrHashSet, obliviousItemType, out var isFinal );
                        if( !isFinal )
                        {
                            Throw.DebugAssert( oTypeName == $"{listOrHashSet}<{obliviousItemType.CSharpName}>" );
                            if( !_typeCache.TryGetValue( oTypeName, out finalType ) )
                            {
                                // The final type is a regular List (or HashSet) of oblivious item.
                                // When the item is an anonymous record, then it is unnamed (oblivious => unnamed).
                                // The final type is its own RegularCollection.
                                var tFinal = (isList ? typeof( List<> ) : typeof( HashSet<> ) ).MakeGenericType( obliviousItemType.Type );
                                finalType = PocoType.CreateCollection( this,
                                                                       tFinal,
                                                                       oTypeName,
                                                                       oTypeName,
                                                                       isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                       obliviousItemType,
                                                                       obliviousType: null,
                                                                       finalType: null,
                                                                       regularCollection: null );
                                Throw.DebugAssert( !finalType.IsNullable );
                                _typeCache.Add( oTypeName, finalType );
                                // Final type is oblivious: as reference type it is nullable.
                                _typeCache.Add( tFinal, finalType.Nullable );
                            }
                            // Both lookup (by name) and creation returns the non nullable.
                            finalType = finalType.Nullable;
                            Throw.DebugAssert( finalType.IsOblivious && finalType.IsStructuralFinalType
                                               && !(finalType.NonNullable.IsOblivious || finalType.NonNullable.IsStructuralFinalType) );
                        }
                    }
                    // The regular collection may be available but it is not necessarily the oblivious's one.
                    // If a final type has been computed (because this oblivious is not final) then the
                    // oblivious.RegularCollection is the final type.
                    // When no final type is available (because this oblivious is final) it may be the resolved
                    // regular collection if its item type happens to be oblivious.
                    var obliviousRegular = finalType?.NonNullable
                                           ?? (regularCollection?.ItemTypes[0] == obliviousItemType ? regularCollection : null);

                    Throw.DebugAssert( "The obliviousItemType is necessarily compliant with the regular collection (oblivious => unnamed record).",
                                       obliviousItemType is not IAnonymousRecordPocoType a1 || a1.IsUnnamed );

                    // The only reason why the oblivious cannot be its own regular collection is because we are building an abstraction:
                    // bool obliviousWillBeRegular = isRegular;
                    if( obliviousRegular == null && !isRegular )
                    {
                        // We must create the regular collection: recursive call here (but will be only a single reentrancy).
                        var nRegular = nType.SetReferenceTypeDefinition( isList ? typeof( List<> ) : typeof( HashSet<> ) ).ToNonNullable();
                        obliviousRegular = RegisterListOrSetCore( isList, nRegular, true, listOrHashSet, obliviousItemType, null );
                    }
                    obliviousType = PocoType.CreateCollection( this,
                                                               t,
                                                               oName,
                                                               oTypeName,
                                                               isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                               itemType: obliviousItemType,
                                                               obliviousType: null,
                                                               finalType,
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

            string GetAbstractionImplTypeSupport( bool isList, string listOrHashSet, IPocoType tI, out bool isFinal )
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
                // If there is no specific support implementation, the regular and final List or HashSet is
                // the implementation type.
                isFinal = typeName != null;
                if( !isFinal )
                {
                    typeName = $"{listOrHashSet}<{tI.CSharpName}>";
                }
                return typeName!;
            }
        }

        IPocoType? RegisterItemType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, bool isRegular, string listOrHashSet )
        {
            bool valid = ctx.EnterListSetOrDictionary( monitor, nType, isRegular, listOrHashSet );
            var tI = Register( monitor, ctx, nType.GenericTypeArguments[0] );
            return valid ? tI : null;
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
