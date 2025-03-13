using CK.Core;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System;
using System.Runtime.CompilerServices;

namespace CK.Setup;

public sealed partial class PocoTypeSystemBuilder
{
    IPocoType? RegisterDictionary( IActivityMonitor monitor, IExtNullabilityInfo nType, MemberContext ctx, bool isConcrete )
    {
        if( !RegisterKeyAndValueTypes( monitor, nType, ctx, isConcrete, out var tK, out var tV )
            || !CheckDictionaryKeyType( monitor, nType, ctx, tK ) )
        {
            return null;
        }
        var t = nType.Type;
        if( isConcrete )
        {
            var c = RegisterConcreteDictionary( t, tK, tV );
            return nType.IsNullable ? c.Nullable : c;
        }

        // Type erasure of SecondaryPoco to PrimaryPoco for abstract collection only.
        if( tV.Kind == PocoTypeKind.SecondaryPoco )
        {
            tV = Unsafe.As<ISecondaryPocoType>( tV ).PrimaryPocoType;
            t = typeof( IDictionary<,> ).MakeGenericType( tK.Type, tV.Type );
        }
        var concreteType = typeof( Dictionary<,> ).MakeGenericType( tK.Type, tV.Type );
        var concreteCollection = RegisterConcreteDictionary( concreteType, tK, tV );
        var result = RegisterAbstractDictionary( t, tK, tV, concreteCollection );
        return nType.IsNullable ? result.Nullable : result;
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


    IPocoType RegisterConcreteDictionary( Type t, IPocoType tK, IPocoType tV )
    {
        Throw.DebugAssert( "Only abstract read only collections can have a null regular and a read only collection cannot be a collection key or value",
                           tK.RegularType != null && tV.RegularType != null );
        IPocoType? nonSecondaryConcreteCollection = null;
        ICollectionPocoType? regularCollection = null;
        IPocoType tKRegular = tK.RegularType;
        if( tV is ISecondaryPocoType sec )
        {
            Throw.DebugAssert( "C# type is the same.", tK.Type == tKRegular.Type );
            var tDicPrimary = typeof( Dictionary<,> ).MakeGenericType( tK.Type, sec.PrimaryPocoType.Type );

            ICollectionPocoType? nsRegularCollection = null;
            if( tK != tKRegular )
            {
                nsRegularCollection = Unsafe.As<ICollectionPocoType>( DoRegisterConcreteDictionary( tDicPrimary, tKRegular, sec.PrimaryPocoType, null, null ) );

                regularCollection = Unsafe.As<ICollectionPocoType>( DoRegisterConcreteDictionary( t, tKRegular, tV, null, nsRegularCollection ) );
            }
            nonSecondaryConcreteCollection = DoRegisterConcreteDictionary( tDicPrimary, tK, sec.PrimaryPocoType, nsRegularCollection, null );
        }
        else
        {
            IPocoType tVRegular = tV.RegularType;
            if( tK != tKRegular || tV != tVRegular )
            {
                Throw.DebugAssert( "C# types are the same.", tK.Type == tKRegular.Type && tV.Type == tVRegular.Type );
                regularCollection = Unsafe.As<ICollectionPocoType>( DoRegisterConcreteDictionary( t, tKRegular, tVRegular, null, null ) );
            }
        }
        return DoRegisterConcreteDictionary( t, tK, tV, regularCollection, nonSecondaryConcreteCollection );
    }

    IPocoType DoRegisterConcreteDictionary( Type t,
                                            IPocoType tK,
                                            IPocoType tV,
                                            ICollectionPocoType? regularCollection,
                                            IPocoType? nonSecondaryConcreteCollection )
    {
        var csharpName = $"Dictionary<{tK.CSharpName},{tV.CSharpName}>";
        if( !_typeCache.TryGetValue( csharpName, out var result ) )
        {
            if( !_typeCache.TryGetValue( t, out var obliviousType ) )
            {
                // The dictionary key is not necessarily oblivious: it must always be not null.
                // For anonymous record, we need to consider the oblivious (and this one, as a value type) is non nullable,
                // but for reference type, we must take the non nullable.
                // Following ObliviousType.NonNullable always correctly adapts the key type.
                var obliviousKeyType = tK.ObliviousType.NonNullable;
                var obliviousValueType = tV.ObliviousType;
                // The regular is Oblivious and Final.
                var oName = $"Dictionary<{obliviousKeyType.CSharpName},{obliviousValueType.CSharpName}>";

                // If the regular collection is available and its item type happens to be oblivious then
                // it is the regular collection.
                var obliviousRegular = regularCollection?.ItemTypes[0] == obliviousKeyType && regularCollection?.ItemTypes[1] == obliviousValueType
                                            ? regularCollection
                                            : null;

                Throw.DebugAssert( "The oblivious item types are necessarily compliant with the regular collection: " +
                                   "oblivious => regular (or null regular for abstract read only but we are not in this case).",
                                    obliviousKeyType.IsRegular && obliviousValueType.IsRegular );

                obliviousType = PocoType.CreateDictionary( this,
                                                           t,
                                                           oName,
                                                           oName,
                                                           obliviousKeyType,
                                                           obliviousValueType,
                                                           obliviousType: null,
                                                           finalType: null,
                                                           obliviousRegular,
                                                           nonSecondaryConcreteCollection?.ObliviousType.NonNullable ).Nullable;
                _typeCache.Add( t, obliviousType );
                _typeCache.Add( oName, obliviousType.NonNullable );
            }
            Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.IsNullable );
            // We are the oblivious if the value is oblivious.
            if( tV.IsOblivious )
            {
                result = obliviousType.NonNullable;
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
                                                    regularCollection,
                                                    nonSecondaryConcreteCollection );
                _typeCache.Add( csharpName, result );
            }
        }
        Throw.DebugAssert( !result.IsNullable );
        return result;
    }


    IPocoType RegisterAbstractDictionary( Type t,
                                          IPocoType tK,
                                          IPocoType tV,
                                          IPocoType concreteCollection )
    {
        var csharpName = $"IDictionary<{tK.CSharpName},{tV.CSharpName}>";
        if( !_typeCache.TryGetValue( csharpName, out var result ) )
        {
            if( !_typeCache.TryGetValue( t, out var obliviousType ) )
            {
                var obliviousKeyType = tK.ObliviousType.NonNullable;
                var obliviousValueType = tV.ObliviousType;
                var oName = $"IDictionary<{obliviousKeyType.CSharpName},{obliviousValueType.CSharpName}>";
                var oTypeName = GetAbstractionImplTypeSupport( obliviousKeyType, obliviousValueType );
                result = PocoType.CreateAbstractCollection( this,
                                                            t,
                                                            oName,
                                                            oTypeName ?? concreteCollection.ImplTypeName,
                                                            PocoTypeKind.Dictionary,
                                                            concreteCollection: concreteCollection.ObliviousType.NonNullable,
                                                            obliviousType: null,
                                                            oTypeName == null ? concreteCollection.StructuralFinalType : null );
                Throw.DebugAssert( !result.IsNullable );
                _typeCache.Add( oName, result );
                obliviousType = result.Nullable;
                _typeCache.Add( t, obliviousType );
            }
            // We are the oblivious if the value is oblivious.
            if( tV.IsOblivious )
            {
                result = obliviousType.NonNullable;
            }
            else
            {
                result = PocoType.CreateAbstractCollection( this,
                                                            t,
                                                            csharpName,
                                                            obliviousType.ImplTypeName,
                                                            obliviousType.Kind,
                                                            concreteCollection,
                                                            obliviousType,
                                                            obliviousType.StructuralFinalType );
                Throw.DebugAssert( result.ImplTypeName != null && result.ImplTypeName == obliviousType.ImplTypeName );
                Throw.DebugAssert( !result.IsNullable );
                _typeCache.Add( csharpName, result );
            }
        }
        return result;

        string? GetAbstractionImplTypeSupport( IPocoType tK, IPocoType tV )
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
            return typeName;
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
