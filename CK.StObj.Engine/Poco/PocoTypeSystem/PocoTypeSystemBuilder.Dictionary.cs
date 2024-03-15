using CK.Core;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystemBuilder
    {
        IPocoType? RegisterDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, bool isRegular )
        {
            if( RegisterKeyAndValueTypes( monitor, nType, ctx, isRegular, out var tK, out var tV ) )
            {
                if( !CheckDictionaryKeyType( monitor, nType, ctx, tK ) )
                {
                    return null;
                }
                return RegisterDictionaryCore( monitor, nType, isRegular, tK, tV );
            }
            return null;
        }

        IPocoType? RegisterReadOnlyDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx )
        {
            if( !RegisterKeyAndValueTypes( monitor, nType, ctx, false, out var tK, out var tV ) )
            {
                return null;
            }
            // IReadOnlyDictionary<object,...> is allowed.
            if( tK.Kind != PocoTypeKind.Any && !CheckDictionaryKeyType( monitor, nType, ctx, tK ) )
            {
                return null;
            }
            var csharpName = $"IReadOnlyDictionary<{tK.CSharpName},{tV.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                IPocoType? obliviousType = null;
                if( !tK.IsOblivious || !tV.IsOblivious )
                {
                    var tOblivious = typeof( IReadOnlyDictionary<,> ).MakeGenericType( tK.ObliviousType.Type, tV.ObliviousType.Type );
                    if( _typeCache.TryGetValue( tOblivious, out obliviousType ) )
                    {
                        var obliviousTypeName = $"IReadOnlyDictionary<{tK.ObliviousType.CSharpName},{tV.ObliviousType.CSharpName}>";
                        obliviousType = PocoType.CreateAbstractCollection( this,
                                                                           tOblivious,
                                                                           obliviousTypeName,
                                                                           PocoTypeKind.Dictionary,
                                                                           new[] { tK.ObliviousType, tV.ObliviousType },
                                                                           null );
                        _typeCache.Add( obliviousTypeName, obliviousType );
                        _typeCache.Add( tOblivious, obliviousType );
                    }
                }
                result = PocoType.CreateAbstractCollection( this,
                                                            nType.Type,
                                                            csharpName,
                                                            PocoTypeKind.Dictionary,
                                                            new[] { tK, tV },
                                                            obliviousType );
                if( obliviousType == null )
                {
                    _typeCache.Add( result.Type, result );
                }
                _typeCache.Add( csharpName, result );
            }
            return nType.IsNullable ? result.Nullable : result;
        }

        static bool CheckDictionaryKeyType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, IPocoType tK )
        {
            if( !tK.IsHashSafe || tK.IsPolymorphic )
            {
                var error = tK.IsPolymorphic ? "polymorphic" : "not \"hash safe\"";
                monitor.Error( $"{ctx}: '{nType.Type:C}' key cannot be '{tK.CSharpName}' because this type is {error}." );
                return false;
            }
            return true;
        }

        bool RegisterKeyAndValueTypes( IActivityMonitor monitor,
                                       IExtNullabilityInfo nType,
                                       MemberContext ctx,
                                       bool isRegular,
                                       [NotNullWhen( true )] out IPocoType? tK,
                                       [NotNullWhen( true )] out IPocoType? tV )
        {
            tV = null;
            bool valid = ctx.EnterListSetOrDictionary( monitor, nType, isRegular, "Dictionary" );
            tK = Register( monitor, ctx, nType.GenericTypeArguments[0] );
            if( tK == null ) return false;
            if( tK.IsNullable )
            {
                monitor.Error( $"{ctx}: '{nType.Type:C}' key cannot be nullable. Nullable type '{tK.CSharpName}' cannot be a key." );
                return false;
            }
            tV = Register( monitor, ctx, nType.GenericTypeArguments[1] );
            if( tV == null || !valid ) return false;
            return true;
        }

        IPocoType RegisterDictionaryCore( IActivityMonitor monitor, IExtNullabilityInfo nType, bool isRegular, IPocoType tK, IPocoType tV )
        {
            var csharpName = isRegular
                                ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                var t = nType.Type;
                Type? tOblivious = null;
                string? typeName = null;
                if( !isRegular )
                {
                    if( tV.Type.IsValueType )
                    {
                        Throw.DebugAssert( "Value types are implemented by themselves.", tV.ImplTypeName == tV.CSharpName );
                        if( tV.IsNullable )
                        {
                            Throw.DebugAssert( typeof( CovariantHelpers.CovNullableValueDictionary<,> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                                == "CK.Core.CovariantHelpers.CovNullableValueDictionary<,>" );
                            typeName = $"CovariantHelpers.CovNullableValueDictionary<{tK.ImplTypeName},{tV.NonNullable.ObliviousType.ImplTypeName}>";
                            t = typeof( CovariantHelpers.CovNullableValueDictionary<,> ).MakeGenericType( tK.Type, tV.NonNullable.Type );
                        }
                        else
                        {
                            Throw.DebugAssert( typeof( CovariantHelpers.CovNotNullValueDictionary<,> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                                == "CK.Core.CovariantHelpers.CovNotNullValueDictionary<,>" );
                            typeName = $"CovariantHelpers.CovNotNullValueDictionary<{tK.ImplTypeName},{tV.ObliviousType.ImplTypeName}>";
                            t = typeof( CovariantHelpers.CovNotNullValueDictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                        }
                        tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.ObliviousType.Type, tV.ObliviousType.Type );
                    }
                    else
                    {
                        bool isSecondary = tV.Kind == PocoTypeKind.SecondaryPoco;
                        if( isSecondary || tV.Kind == PocoTypeKind.PrimaryPoco )
                        {
                            // The adapter enables Primary and Secondary inputs and AbstractPoco outputs.
                            var poco = isSecondary
                                            ? ((ISecondaryPocoType)tV.NonNullable).PrimaryPocoType
                                            : (IPrimaryPocoType)tV.NonNullable;
                            typeName = EnsurePocoDictionaryType( tK, poco );
                            t = IDynamicAssembly.PurelyGeneratedType;
                            tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.ObliviousType.Type, tV.Type );
                        }
                        else 
                        {
                            if( tV.Kind != PocoTypeKind.Any )
                            {
                                // IReadOnlyDictionary<TKey,TValue> is NOT covariant on TValue: we always need an adapter.
                                // We support it here for AbstractPoco, string and other basic reference types.
                                typeName = EnsurePocoDictionaryOfAbstractOrBasicRefType( tK, tV.NonNullable );
                                t = IDynamicAssembly.PurelyGeneratedType;
                            }
                            tOblivious = typeof( Dictionary<,> ).MakeGenericType( tK.ObliviousType.Type, tV.ObliviousType.Type );
                        }
                    }                    
                }
                if( typeName == null )
                {
                    t = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                    if( isRegular && tV.IsOblivious )
                    {
                        typeName = csharpName;
                    }
                    else
                    {
                        tOblivious = t;
                        typeName = isRegular ? csharpName : $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                    }
                }
                IPocoType? obliviousType = null;
                if( tOblivious != null )
                {
                    if( !_typeCache.TryGetValue( tOblivious, out obliviousType ) )
                    {
                        var obliviousTypeName = $"Dictionary<{tK.ObliviousType.CSharpName},{tV.ObliviousType.CSharpName}>";
                        Throw.DebugAssert( "The only way for the typeName to be the oblivious one here is if a IDictionary<,> is requested.",
                                            typeName != obliviousTypeName || !isRegular );
                        obliviousType = PocoType.CreateDictionary( this,
                                                                   tOblivious,
                                                                   obliviousTypeName,
                                                                   obliviousTypeName,
                                                                   tK.ObliviousType,
                                                                   tV.ObliviousType,
                                                                   null );
                        _typeCache.Add( tOblivious, obliviousType );
                        _typeCache.Add( obliviousTypeName, obliviousType );
                    }
                    Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.CSharpName == $"Dictionary<{tK.ObliviousType.CSharpName},{tV.ObliviousType.CSharpName}>" );
                }
                Throw.DebugAssert( "We have the oblivious type or we are creating it.", obliviousType != null || typeName == csharpName );
                result = PocoType.CreateDictionary( this,
                                                    t,
                                                    csharpName,
                                                    typeName,
                                                    tK,
                                                    tV,
                                                    obliviousType );
                _typeCache.Add( csharpName, result );
                if( obliviousType == null )
                {
                    Throw.DebugAssert( result.IsOblivious && csharpName == typeName );
                    _typeCache.Add( result.Type, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result;
        }

        string EnsurePocoDictionaryType( IPocoType tK, IPrimaryPocoType tV )
        {
            Debug.Assert( !tV.IsNullable );
            var genTypeName = $"PocoDictionary_{tK.Index}_{tV.Index}_CK";
            if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
            {
                g = new PocoDictionaryRequiredSupport( tK, tV, genTypeName );
                _requiredSupportTypes.Add( genTypeName, g );
            }
            return g.FullName;
        }

        string EnsurePocoDictionaryOfAbstractOrBasicRefType( IPocoType tK, IPocoType tV )
        {
            Debug.Assert( !tV.IsNullable );
            var genTypeName = $"PocoDictionary_{tK.Index}_{tV.Index}_CK";
            if( !_requiredSupportTypes.TryGetValue( genTypeName, out var g ) )
            {
                g = new PocoDictionaryOfAbstractOrBasicRefRequiredSupport( tK, tV, genTypeName );
                _requiredSupportTypes.Add( genTypeName, g );
            }
            return g.FullName;
        }
    }

}
