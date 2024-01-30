using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Collections;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystemBuilder
    {
        IPocoType? RegisterListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx, bool isRegular )
        {
            var listOrHashSet = isList ? "List" : "HashSet";
            var tI = RegisterItemType( monitor, nType, ctx, isRegular, listOrHashSet );
            if( tI == null ) return null;
            return RegisterListOrSetCore( monitor, isList, nType, isRegular, listOrHashSet, tI );
        }

        IPocoType? RegisterReadOnlyListOrSet( IActivityMonitor monitor, bool isList, IExtNullabilityInfo nType, MemberContext ctx )
        {
            var listOrHashSet = isList ? "List" : "HashSet";
            var tI = RegisterItemType( monitor, nType, ctx, false, listOrHashSet );
            if( tI == null ) return null;

            var csharpName = $"IReadOnly{(isList ? "List" : "Set")}<{tI.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                var mType = nType.SetReferenceTypeDefinition( isList ? typeof( IList<> ) : typeof( ISet<> ), nullable: false );
                var mutable = RegisterListOrSetCore( monitor, isList, mType, false, listOrHashSet, tI );
                Throw.DebugAssert( "RegisterListOrSetCore cannot fail.", mutable is ICollectionPocoType );
                result = PocoType.CreateAbstractCollection( this, nType.Type, csharpName, (ICollectionPocoType)mutable );
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
                var t = nType.Type;
                Type? tOblivious = null;
                string? typeName = null;
                if( !isRegular )
                {
                    // IList or ISet
                    if( tI.Type.IsValueType )
                    {
                        Throw.DebugAssert( "Value types are implemented by themselves.", tI.CSharpName == tI.ImplTypeName );
                        // For value type item, use our covariant implementations.
                        // We use the Oblivious type name as a minor optimization for Roslyn here when the item
                        // is an anonymous record: instead of using the CSharName with its field names that will
                        // create useless TupleNamesAttribute, the oblivious has no field names.
                        if( tI.IsNullable )
                        {
                            typeName = $"CovariantHelpers.CovNullableValue{listOrHashSet}<{tI.NonNullable.ObliviousType.ImplTypeName}>";
                            t = (isList ? typeof( CovariantHelpers.CovNullableValueList<> ) : typeof( CovariantHelpers.CovNullableValueHashSet<> ))
                                .MakeGenericType( tI.NonNullable.Type );
                        }
                        else
                        {
                            typeName = $"CovariantHelpers.CovNotNullValue{listOrHashSet}<{tI.ObliviousType.ImplTypeName}>";
                            t = (isList ? typeof( CovariantHelpers.CovNotNullValueList<> ) : (typeof( CovariantHelpers.CovNotNullValueHashSet<> )))
                                .MakeGenericType( tI.Type );
                        }
                        // We are obviously not the oblivious.
                        tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                    }
                    else
                    {
                        bool isSecondary = tI.Kind == PocoTypeKind.SecondaryPoco;
                        if( isSecondary || tI.Kind == PocoTypeKind.PrimaryPoco )
                        {
                            // For IPoco, use generated covariant implementations only if needed:
                            // - For list only if more than one Poco interface exist in the family. When the family contains only one interface
                            //   (the primary one), the oblivious List<PrimaryInterface> is fine.
                            // - But it's not the case for Set because IReadOnlySet<T> is NOT covariant. We need the object and abstract adaptations...
                            var poco = isSecondary
                                        ? ((ISecondaryPocoType)tI.NonNullable).PrimaryPocoType
                                        : (IPrimaryPocoType)tI.NonNullable;
                            if( !isList || isSecondary || poco.FamilyInfo.Interfaces.Count > 1 )
                            {
                                // We choose the non nullable item type to follow the C# "oblivious nullable reference type" that is non nullable. 
                                typeName = EnsurePocoListOrHashSetType( monitor, poco, isList, listOrHashSet );
                                t = IDynamicAssembly.PurelyGeneratedType;
                                // Since we are on a reference type, the oblivious is the non nullable.
                                Debug.Assert( poco.IsOblivious );
                                // We are obviously not the oblivious.
                                tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                            }
                        }
                        else if( tI.Kind != PocoTypeKind.Any )
                        {
                            // HashSet<> is not natively covariant. We support it here for
                            // AbstractPoco, string and other basic reference types.
                            if( !isList )
                            {
                                typeName = EnsurePocoHashSetOfAbstractOrBasicRefType( monitor, tI.NonNullable );
                                t = IDynamicAssembly.PurelyGeneratedType;
                                // We are obviously not the oblivious.
                                tOblivious = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                            }
                        }
                    }
                }
                if( typeName == null )
                {
                    // It's not an abstraction for which we have a dedicated implementation or it's
                    // explicitly a regular List/HashSet: use the regular collection type.
                    t = (isList ? typeof( List<> ) : typeof( HashSet<> )).MakeGenericType( tI.Type );
                    // This is the oblivious implementation if:
                    //   - the regular type has been requested
                    //   - AND the item type is the oblivious one.
                    if( isRegular && tI.IsOblivious )
                    {
                        // We are building the oblivious: let the tOblivious be null.
                        typeName = csharpName;
                    }
                    else
                    {
                        tOblivious = t;
                        typeName = isRegular ? csharpName : $"{listOrHashSet}<{tI.CSharpName}>";
                    }
                }
                // Ensure that the obliviousType is registered if we are not instantiating it.
                IPocoType? obliviousType = null;
                if( tOblivious != null )
                {
                    // The type we are about to create is not the oblivious one.
                    // However we have everything here to create it:
                    // - It has the same kind.
                    // - Its typeName is its obliviousTypeName.
                    // - Its item type is tI.ObliviousType.
                    // - We used the tOblivious != null as a flag, so we have it.
                    if( !_typeCache.TryGetValue( tOblivious, out obliviousType ) )
                    {
                        var obliviousTypeName = $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>";
                        Throw.DebugAssert( "The only way for the typeName to be the oblivious one here is if a IList<> or ISet<> is requested.",
                                           typeName != obliviousTypeName || !isRegular );
                        obliviousType = PocoType.CreateCollection( monitor,
                                                                   this,
                                                                   tOblivious,
                                                                   obliviousTypeName,
                                                                   obliviousTypeName,
                                                                   isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                                   itemType: tI.ObliviousType,
                                                                   obliviousType: null );
                        _typeCache.Add( tOblivious, obliviousType );
                        _typeCache.Add( obliviousTypeName, obliviousType );
                    }
                    Debug.Assert( obliviousType.IsOblivious && obliviousType.CSharpName == $"{listOrHashSet}<{tI.ObliviousType.CSharpName}>" );
                }
                Debug.Assert( obliviousType != null || typeName == csharpName, "We have the oblivious type or we are creating it." );
                result = PocoType.CreateCollection( monitor,
                                                    this,
                                                    t,
                                                    csharpName,
                                                    typeName,
                                                    isList ? PocoTypeKind.List : PocoTypeKind.HashSet,
                                                    tI,
                                                    obliviousType );
                _typeCache.Add( csharpName, result );
                // If we have built the oblivious, register it.
                if( obliviousType == null )
                {
                    Throw.DebugAssert( result.IsOblivious && csharpName == typeName );
                    _typeCache.Add( result.Type, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result;
        }

        IPocoType? RegisterItemType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, bool isRegular, string listOrHashSet )
        {
            bool valid = ctx.EnterListSetOrDictionary( monitor, nType, isRegular, listOrHashSet );
            var tI = Register( monitor, ctx, nType.GenericTypeArguments[0] );
            return valid ? tI : null;
        }

        string EnsurePocoListOrHashSetType( IActivityMonitor monitor, IPrimaryPocoType tI, bool isList, string listOrHasSet )
        {
            Debug.Assert( !tI.IsNullable );
            var genTypeName = $"Poco{listOrHasSet}_{tI.Index}_CK";
            if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
            {
                _requiredSupportTypes.Add( genTypeName, g = new PocoListOrHashSetRequiredSupport( tI, genTypeName, isList ) );
            }
            return g.FullName;
        }

        string EnsurePocoHashSetOfAbstractOrBasicRefType( IActivityMonitor monitor, IPocoType tI )
        {
            Debug.Assert( !tI.IsNullable );
            var genTypeName = $"PocoHashSet_{tI.Index}_CK";
            if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
            {
                _requiredSupportTypes.Add( genTypeName, g = new PocoHashSetOfAbstractOrBasicRefRequiredSupport( tI, genTypeName ) );
            }
            return g.FullName;
        }
    }

}
