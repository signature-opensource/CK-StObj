using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using CK.CodeGen;

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
            // existing RegisterListOrSetCore we handle it here. This is not really lovely but this
            // does the job.
            IPocoType? anonymousRegular = null;
            if( tI is IAnonymousRecordPocoType a && !a.IsUnnamed )
            {
                var nRegular = isRegular
                                ? nType
                                : nType.SetReferenceTypeDefinition( isList ? typeof( List<> ) : typeof( HashSet<> ) );
                anonymousRegular = RegisterListOrSetCore( monitor, isList, nRegular, true, listOrHashSet, a.UnnamedRecord );
            }
            return RegisterListOrSetCore( monitor, isList, nType, isRegular, listOrHashSet, tI );
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

        IPocoType RegisterListOrSetCore( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, bool isRegular, string listOrHashSet, IPocoType tI )
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
                        oName = $"{(isList ? "IList" : "ISet")}<{obliviousItemType.CSharpName}>";
                        oTypeName = GetAbstractionImplTypeSupport( isList, listOrHashSet, obliviousItemType, out var isFinal );
                        if( !isFinal )
                        {
                            Throw.DebugAssert( oTypeName == $"{listOrHashSet}<{obliviousItemType.CSharpName}>" );
                            if( !_typeCache.TryGetValue( oTypeName, out finalType ) )
                            {
                                var tFinal = (isList ? typeof( List<> ) : typeof( HashSet<> ) ).MakeGenericType( obliviousItemType.Type );
                                finalType = PocoType.CreateCollection( this,
                                                                       tFinal,
                                                                       oTypeName,
                                                                       oTypeName,
                                                                       isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                       obliviousItemType,
                                                                       obliviousType: null,
                                                                       finalType: null );
                                Throw.DebugAssert( !finalType.IsNullable );
                                _typeCache.Add( oTypeName, finalType );
                                // Final type is oblivious: as reference type it is nullable.
                                finalType = finalType.Nullable;
                                _typeCache.Add( tFinal, finalType );
                            }
                            Throw.DebugAssert( finalType.IsOblivious && finalType.IsStructuralFinalType
                                               && !(finalType.NonNullable.IsOblivious || finalType.NonNullable.IsStructuralFinalType) );
                        }
                    }
                    obliviousType = PocoType.CreateCollection( this,
                                                               t,
                                                               oName,
                                                               oTypeName,
                                                               isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                               itemType: obliviousItemType,
                                                               obliviousType: null,
                                                               finalType ).ObliviousType;
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
                    result = PocoType.CreateCollection( this,
                                                        t,
                                                        csharpName,
                                                        obliviousType.ImplTypeName,
                                                        obliviousType.Kind,
                                                        tI,
                                                        obliviousType,
                                                        obliviousType.StructuralFinalType );
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
