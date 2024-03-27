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
                Throw.DebugAssert( "Only abstract read only collections can have a null regular and a read only collection cannot be a key or a value",
                                   tK.RegularType != null && tV.RegularType != null );

                ICollectionPocoType? regularCollection = null;
                IPocoType tKRegular = tK.RegularType;
                IPocoType tVRegular = tV.RegularType;
                if( !isRegular || tK != tKRegular || tV != tVRegular )
                {
                    var nRegular = isRegular
                                    ? nType
                                    : nType.SetReferenceTypeDefinition( typeof( Dictionary<,> ) );
                    regularCollection = Unsafe.As<ICollectionPocoType>( RegisterDictionaryCore( nRegular.ToNonNullable(), true, tKRegular, tVRegular, null ) );
                }
                return RegisterDictionaryCore( nType, isRegular, tK, tV, regularCollection );
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
                Throw.DebugAssert( "Key type is what it is: either object (non nullable) or non necessarily oblivious but non nullable, " +
                                   "hash safe and non polymorphic.",
                                   !tK.IsNullable && (tK.Kind == PocoTypeKind.Any || (tK.IsReadOnlyCompliant && !tK.IsPolymorphic)) );
                IPocoType? obliviousType = null;
                if( !tV.IsOblivious )
                {
                    var tOblivious = typeof( IReadOnlyDictionary<,> ).MakeGenericType( tK.Type, tV.ObliviousType.Type );
                    if( _typeCache.TryGetValue( tOblivious, out obliviousType ) )
                    {
                        var obliviousTypeName = $"IReadOnlyDictionary<{tK.CSharpName},{tV.ObliviousType.CSharpName}>";
                        obliviousType = PocoType.CreateAbstractCollection( this,
                                                                           tOblivious,
                                                                           obliviousTypeName,
                                                                           PocoTypeKind.Dictionary,
                                                                           new[] { tK, tV.ObliviousType },
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

        static bool CheckDictionaryKeyType( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, IPocoType tK )
        {
            if( !tK.IsReadOnlyCompliant || tK.IsPolymorphic )
            {
                var error = tK.IsPolymorphic ? "polymorphic" : "not read-only compliant";
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

        IPocoType RegisterDictionaryCore( IExtNullabilityInfo nType,
                                          bool isRegular,
                                          IPocoType tK,
                                          IPocoType tV,
                                          ICollectionPocoType? regularCollection )
        {
            var csharpName = isRegular
                                ? $"Dictionary<{tK.CSharpName},{tV.CSharpName}>"
                                : $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
            if( !_typeCache.TryGetValue( csharpName, out var result ) )
            {
                Type t = nType.Type;
                // Type erasure of SecondaryPoco to PrimaryPoco for abstract collection only.
                var obliviousValueType = tV.ObliviousType;
                if( !isRegular && obliviousValueType.Kind == PocoTypeKind.SecondaryPoco )
                {
                    obliviousValueType = Unsafe.As<ISecondaryPocoType>( obliviousValueType ).PrimaryPocoType;
                    Throw.DebugAssert( obliviousValueType.IsOblivious );
                    t = typeof( IDictionary<,> ).MakeGenericType( tK.Type, obliviousValueType.Type );
                }
                if( !_typeCache.TryGetValue( t, out var obliviousType ) )
                {
                    IPocoType? finalType = null;
                    string oName, oTypeName;
                    // The dictionary key is not necessarily oblivious: it must always be not null.
                    // For anonymous record, we need to consider the oblivious (and this one, as a value type) is non nullable,
                    // but for reference type, we must take the non nullable.
                    // Following ObliviousType.NonNullable always correctly adapts the key type.
                    var obliviousKeyType = tK.ObliviousType.NonNullable;
                    if( isRegular )
                    {
                        // The regular is Oblivious and Final.
                        oName = $"Dictionary<{obliviousKeyType.CSharpName},{obliviousValueType.CSharpName}>";
                        oTypeName = oName;
                    }
                    else
                    {
                        Throw.DebugAssert( "IDictionary: the regular collection has been created.", regularCollection != null );
                        oName = $"IDictionary<{obliviousKeyType.CSharpName},{obliviousValueType.CSharpName}>";
                        oTypeName = GetAbstractionImplTypeSupport( obliviousKeyType, obliviousValueType, out var isFinal );
                        if( !isFinal )
                        {
                            // The final type is a Dictionary of oblivious key and value.
                            // When item is an anonymous record, then it is unnamed (oblivious => unnamed).
                            // The final type is its own RegularCollection.
                            Throw.DebugAssert( oTypeName == oName.Substring( 1 ) );
                            if( !_typeCache.TryGetValue( oTypeName, out finalType ) )
                            {
                                var tFinal = typeof( Dictionary<,> ).MakeGenericType( tK.Type, obliviousValueType.Type );
                                finalType = PocoType.CreateDictionary( this,
                                                                       tFinal,
                                                                       oTypeName,
                                                                       oTypeName,
                                                                       obliviousKeyType,
                                                                       obliviousValueType,
                                                                       obliviousType: null,
                                                                       finalType: null,
                                                                       regularCollectionType: null );
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
                    // oblivious.RegularCollection is the final.
                    // When no final type is available (because this oblivious is final) it may be the resolved
                    // regular collection if its item type happens to be oblivious.
                    var obliviousRegular = finalType?.NonNullable
                                            ?? (regularCollection?.ItemTypes[0] == obliviousKeyType && regularCollection?.ItemTypes[1] == obliviousValueType
                                                    ? regularCollection
                                                    : null);

                    Throw.DebugAssert( "The oblivious item types are necessarily compliant with the regular collection: " +
                                       "oblivious => regular (or null regular for abstract read only but we are not in this case).",
                                        obliviousKeyType.IsRegular && obliviousValueType.IsRegular );

                    // The only reason why the oblivious cannot be its own regular collection is because we are building an abstraction.
                    if( obliviousRegular == null && !isRegular )
                    {
                        // We must create the regular collection: recursive call here (but will be only a single reentrancy).
                        var nRegular = nType.SetReferenceTypeDefinition( typeof( Dictionary<,> ) ).ToNonNullable();
                        obliviousRegular = RegisterDictionaryCore( nRegular, true, obliviousKeyType, obliviousValueType, null );
                    }

                    obliviousType = PocoType.CreateDictionary( this,
                                                               t,
                                                               oName,
                                                               oTypeName,
                                                               obliviousKeyType,
                                                               obliviousValueType,
                                                               obliviousType: null,
                                                               finalType,
                                                               obliviousRegular ).ObliviousType;
                    _typeCache.Add( t, obliviousType );
                    _typeCache.Add( oName, obliviousType.NonNullable );
                }
                // We are the oblivious if the value is oblivious (whatever the isRegular is).
                if( tV.IsOblivious )
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
                                                        obliviousType.StructuralFinalType,
                                                        regularCollection );
                    _typeCache.Add( csharpName, result );
                }
            }
            return nType.IsNullable ? result.Nullable : result.NonNullable;

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
                    Throw.DebugAssert( "We are on the oblivious item...", tV.IsOblivious && tV.IsNullable );
                    Throw.DebugAssert( "...and we erased the Secondary (to its primary).", tV.Kind != PocoTypeKind.SecondaryPoco );
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
            Throw.DebugAssert( tV.IsNullable );
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
            Debug.Assert( tV.IsNullable );
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
