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
            return RegisterListOrSetCore( monitor, isList, nType, isRegular, listOrHashSet, tI );
        }

        static bool CheckHashSetItemType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, IPocoType tI )
        {
            if( !tI.IsHashSafe )
            {
                monitor.Error( $"{ctx}: '{nType.Type:C}' item type cannot be '{tI.CSharpName}' because this type is not \"hash safe\"." );
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
                    var tOblivious = (isList ? typeof( IReadOnlyList<> ) : typeof( IReadOnlySet<> ))
                                        .MakeGenericType( tI.ObliviousType.Type );
                    if( _typeCache.TryGetValue( tOblivious, out obliviousType ) )
                    {
                        var obliviousTypeName = $"IReadOnly{(isList ? "List" : "Set")}<{tI.ObliviousType.CSharpName}>";
                        obliviousType = PocoType.CreateAbstractCollection( this,
                                                                           tOblivious,
                                                                           obliviousTypeName,
                                                                           isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                           new[] { tI.ObliviousType },
                                                                           null );
                        _typeCache.Add( obliviousTypeName, obliviousType );
                        _typeCache.Add( tOblivious, obliviousType );
                    }
                }

                result = PocoType.CreateAbstractCollection( this,
                                                            nType.Type,
                                                            csharpName,
                                                            isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                            new[] { tI },
                                                            obliviousType );
                if( obliviousType == null )
                {
                    _typeCache.Add( result.Type, result );
                }
                _typeCache.Add( csharpName, result );
            }
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
                if( !isRegular && tI.Kind == PocoTypeKind.SecondaryPoco )
                {
                    Throw.DebugAssert( tI.ObliviousType is IPrimaryPocoType );
                    t = (isList ? typeof( IList<> ) : typeof( ISet<> )).MakeGenericType( tI.ObliviousType.Type );
                }
                if( !_typeCache.TryGetValue( t, out var obliviousType ) )
                {
                    IPocoType? finalType = null;
                    string oName, oTypeName;
                    if( isRegular )
                    {
                        oName = $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>";
                        oTypeName = oName;
                    }
                    else
                    {
                        oName = $"{(isList ? "IList" : "ISet")}<{tI.ObliviousType.CSharpName}>";
                        oTypeName = GetAbstractionImplTypeSupport( isList, listOrHashSet, tI.ObliviousType, out var isFinal );
                        if( !isFinal )
                        {
                            Throw.DebugAssert( oTypeName == $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>" );
                            if( !_typeCache.TryGetValue( oTypeName, out finalType ) )
                            {
                                var tFinal = (isList ? typeof( List<> ) : typeof( HashSet<> ) ).MakeGenericType( tI.Type );
                                finalType = PocoType.CreateCollection( this,
                                                                       tFinal,
                                                                       oTypeName,
                                                                       oTypeName,
                                                                       isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                       tI.ObliviousType,
                                                                       obliviousType: null,
                                                                       finalType: null );
                                _typeCache.Add( tFinal, finalType );
                                _typeCache.Add( oTypeName, finalType );
                            }
                        }

                    }
                    obliviousType = PocoType.CreateCollection( this,
                                                               t,
                                                               oName,
                                                               oTypeName,
                                                               isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                               itemType: tI.ObliviousType,
                                                               obliviousType: null,
                                                               finalType );
                    _typeCache.Add( t, obliviousType );
                    _typeCache.Add( oName, obliviousType );
                }
                Throw.DebugAssert( "We are the oblivious if the item is oblivious.", tI.IsOblivious == (obliviousType.CSharpName == csharpName) );
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
                    _typeCache.Add( csharpName, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result;

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
                    Throw.DebugAssert( "We are on the oblivious item.", tI.Kind != PocoTypeKind.SecondaryPoco && !tI.IsNullable );
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
            Debug.Assert( !tI.IsNullable );
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
            Debug.Assert( !tI.IsNullable );
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
