using CK.Core;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System;
using System.Collections;
using System.Runtime.CompilerServices;

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
                return RegisterDictionaryCore( nType, isRegular, tK, tV );
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

        IPocoType RegisterDictionaryCore( IExtNullabilityInfo nType, bool isRegular, IPocoType tK, IPocoType tV )
        {
            var csharpName = isRegular
                                ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                Type t = nType.Type;
                // Type erasure of SecondaryPoco to PrimaryPoco for abstract collection only.
                if( !isRegular && tV.Kind == PocoTypeKind.SecondaryPoco )
                {
                    Throw.DebugAssert( tV.ObliviousType is IPrimaryPocoType );
                    t = typeof( IDictionary<,> ).MakeGenericType( tK.Type, tV.ObliviousType.Type );
                }
                if( !_typeCache.TryGetValue( t, out var obliviousType ) )
                {
                    IPocoType? finalType = null;
                    string oName, oTypeName;
                    if( isRegular )
                    {
                        // The regular is the Oblivious and Final type.
                        oName = $"Dictionary<{tK.ObliviousType.CSharpName},{tV.ObliviousType.CSharpName}>";
                        oTypeName = oName;
                    }
                    else
                    {
                        oName = $"IDictionary<{tK.ObliviousType.CSharpName},{tV.ObliviousType.CSharpName}>";
                        oTypeName = GetAbstractionImplTypeSupport( tK.ObliviousType, tV.ObliviousType, out var isFinal );
                        if( !isFinal )
                        {
                            Throw.DebugAssert( oTypeName == oName.Substring( 1 ) );
                            if( !_typeCache.TryGetValue( oTypeName, out finalType ) )
                            {
                                var tFinal = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
                                finalType = PocoType.CreateDictionary( this,
                                                                       tFinal,
                                                                       oTypeName,
                                                                       oTypeName,
                                                                       tK.ObliviousType,
                                                                       tV.ObliviousType,
                                                                       obliviousType: null,
                                                                       finalType: null );
                                _typeCache.Add( tFinal, finalType );
                                Throw.DebugAssert( oTypeName == finalType.CSharpName );
                                _typeCache.Add( oTypeName, finalType );
                            }
                        }
                    }
                    obliviousType = PocoType.CreateDictionary( this,
                                                               t,
                                                               oName,
                                                               oTypeName,
                                                               tK.ObliviousType,
                                                               tV.ObliviousType,
                                                               obliviousType: null,
                                                               finalType );
                    _typeCache.Add( t, obliviousType );
                    _typeCache.Add( oName, obliviousType );
                }
                Throw.DebugAssert( "We are the oblivious if the items are oblivious.",
                                   (tK.IsOblivious && tV.IsOblivious) == (obliviousType.CSharpName == csharpName) );
                if( tK.IsOblivious && tV.IsOblivious )
                {
                    result = obliviousType;
                }
                else
                {
                    result = PocoType.CreateDictionary( this,
                                                        t,
                                                        csharpName,
                                                        obliviousType.ImplTypeName,
                                                        tK,
                                                        tV,
                                                        obliviousType,
                                                        obliviousType.StructuralFinalType );
                    _typeCache.Add( csharpName, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result;

            string GetAbstractionImplTypeSupport( IPocoType tK, IPocoType tV, out bool isFinal )
            {
                string? typeName = null;
                if( tV.Type.IsValueType )
                {
                    Throw.DebugAssert( "Value types are implemented by themselves.", tV.ImplTypeName == tV.CSharpName );
                    if( tV.IsNullable )
                    {
                        Throw.DebugAssert( typeof( CovariantHelpers.CovNullableValueDictionary<,> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                            == "CK.Core.CovariantHelpers.CovNullableValueDictionary<,>" );
                        Throw.DebugAssert( tV.NonNullable.IsOblivious );
                        typeName = $"CovariantHelpers.CovNullableValueDictionary<{tK.ImplTypeName},{tV.NonNullable.ImplTypeName}>";
                    }
                    else
                    {
                        Throw.DebugAssert( typeof( CovariantHelpers.CovNotNullValueDictionary<,> ).ToCSharpName( withNamespace: true, typeDeclaration: false )
                                            == "CK.Core.CovariantHelpers.CovNotNullValueDictionary<,>" );
                        typeName = $"CovariantHelpers.CovNotNullValueDictionary<{tK.ImplTypeName},{tV.ImplTypeName}>";
                    }
                }
                else
                {
                    Throw.DebugAssert( "We are on the oblivious items.", tV.Kind != PocoTypeKind.SecondaryPoco && !tV.IsNullable );
                    if( tV.Kind == PocoTypeKind.PrimaryPoco )
                    {
                        // The adapter enables Primary and Secondary inputs/outputs and AbstractPoco outputs.
                        typeName = EnsurePocoDictionaryType( tK, Unsafe.As<IPrimaryPocoType>( tV ) );
                    }
                    else
                    {
                        if( tV.Kind != PocoTypeKind.Any )
                        {
                            // IReadOnlyDictionary<TKey,TValue> is NOT covariant on TValue: we always need an adapter.
                            // We support it here for AbstractPoco, string and other basic reference types.
                            typeName = EnsurePocoDictionaryOfAbstractOrBasicRefType( tK, tV );
                        }
                    }
                }
                // If there is no specific support implementation, the regular and final Dictionary is
                // the implementation type.
                isFinal = typeName != null;
                if( !isFinal )
                {
                    typeName = $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
                }
                return typeName!;
            }


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
