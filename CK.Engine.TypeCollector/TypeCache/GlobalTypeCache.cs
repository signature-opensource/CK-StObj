using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Global type cache.
/// <para>
/// This currently doesn't handle Nullable Reference Type.
/// </para>
/// </summary>
public sealed partial class GlobalTypeCache
{
    readonly Dictionary<Type, ICachedType> _types;
    readonly AssemblyCache _assemblies;
    readonly ICachedType _iRealObject;
    readonly ICachedType _iPoco;
    readonly ICachedType _iAutoService;
    readonly ICachedType _iAutoServiceScoped;
    readonly ICachedType _iAutoServiceSingleton;
    readonly ICachedType _iAmbientService;

    /// <summary>
    /// Initializes a new cahe for types based on an assembly cache.
    /// </summary>
    /// <param name="assemblies">The assembly cache.</param>
    public GlobalTypeCache( AssemblyCache assemblies )
    {
        _types = new Dictionary<Type, ICachedType>();
        _assemblies = assemblies;

        Type tReal = typeof( IRealObject );
        var abs = assemblies.FindOrCreate( tReal.Assembly );
        var iReal = new CachedType( this, tReal, 0, abs, [], null );
        iReal.SetKind( TypeKindExtension.RealObjectFlags | TypeKind.IsDefiner );
        _types.Add( tReal, _iRealObject = iReal );

        _iPoco = RegisterBase( this, abs, typeof( IPoco ), 0, TypeKind.IsPoco | TypeKind.IsDefiner );
        _iAutoService = RegisterBase( this, abs, typeof( IAutoService ), 0, TypeKind.IsAutoService | TypeKind.IsDefiner );
        _iAutoServiceScoped = RegisterBase( this, abs, typeof( IScopedAutoService ), 1, TypeKind.IsAutoService | TypeKind.IsScoped | TypeKind.IsDefiner );
        _iAutoServiceSingleton = RegisterBase( this, abs, typeof( ISingletonAutoService ), 1, TypeKind.IsSingleton | TypeKind.IsAutoService | TypeKind.IsDefiner );
        _iAmbientService = RegisterBase( this, abs, typeof( IAmbientAutoService ), 2, TypeKind.IsAmbientService | TypeKind.IsContainerConfiguredService | TypeKind.IsScoped | TypeKind.IsAutoService | TypeKind.IsDefiner );

        static CachedType RegisterBase( GlobalTypeCache c, CachedAssembly abs, Type tPoco, int depth, TypeKind kind )
        {
            var iPoco = new CachedType( c, tPoco, depth, abs, [], null );
            iPoco.SetKind( kind );
            c._types.Add( tPoco, iPoco );
            return iPoco;
        }
    }

    public ICachedType IRealObject => _iRealObject;

    public ICachedType IPoco => _iPoco;

    public ICachedType IAutoService => _iAutoService;

    public ICachedType IAutoServiceSingleton => _iAutoServiceSingleton;

    public ICachedType IAutoServiceScoped => _iAutoServiceScoped;

    /// <summary>
    /// Gets a cached type.
    /// <para>
    /// If the type is not yet known, it will be registered.
    /// </para>
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The cached type.</returns>
    public ICachedType Get( Type type ) => Get( type, null );

    /// <summary>
    /// Finds an existing cached type.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The cached type or null.</returns>
    public ICachedType? Find( Type type ) => _types.GetValueOrDefault( type );

    internal ICachedType Get( Type type, CachedAssembly? knownAssembly )
    {
        Throw.CheckArgument( type is not null );
        if( !_types.TryGetValue( type, out ICachedType? c ) )
        {
            knownAssembly ??= _assemblies.FindOrCreate( type.Assembly );
            if( type.IsGenericParameter )
            {
                c = new CachedGenericParameter( this, type, knownAssembly );
                _types.Add( type, c );
                return c;
            }
            // First we must handle Nullable value types.
            Type? nullableValueType = null;
            var isValueType = type.IsValueType;
            if( isValueType )
            {
                var tNotNull = Nullable.GetUnderlyingType( type );
                if( tNotNull != null )
                {
                    nullableValueType = type;
                    type = tNotNull;
                }
                else
                {
                    // "void" is a kind of ValueType.
                    // ref struct are no nullable.
                    // Nullable<T> is not nullable.
                    if( !type.IsByRefLike
                        && type != typeof( void )
                        && type != typeof( Nullable<> ) )
                    {
                        nullableValueType = typeof( Nullable<> ).MakeGenericType( type );
                    }
                }
            }
            // Only then can we work on the type.
            int maxDepth = 0;
            var interfaces = type.GetInterfaces()
                                 .Where( i => i.IsVisible )
                                 .Select( i =>
                                 {
                                     var b = Get( i );
                                     if( maxDepth < b.TypeDepth ) maxDepth = b.TypeDepth;
                                     return b;
                                 } )
                                 .ToImmutableArray();

            ICachedType? baseType = null;
            if( !isValueType )
            {
                Type? tBase = type.BaseType;
                if( tBase != null && tBase != typeof( object ) )
                {
                    var b = Get( tBase );
                    if( maxDepth < b.TypeDepth ) maxDepth = b.TypeDepth;
                    baseType = b;
                }
            }
            // Weird case checks.
            var isAutoService = interfaces.Contains( _iAutoService );
            var isRealObject = interfaces.Contains( _iRealObject );
            var isPoco = interfaces.Contains( _iPoco );
            if( isAutoService || isRealObject || isPoco )
            {
                // Note: we cannot check that a a IPoco must be an interface here without looking
                // for the [StObjGen]. This is done by CachedType.ComputeTypeKind.
                if( !type.IsClass && !type.IsInterface )
                {
                    Throw.CKException( $"Invalid type '{type.ToCSharpName()}': cannot be a IPoco, IRealObject or IAutoService. It must be a class or an interface." );
                }
                if( (isRealObject || isAutoService) && isPoco )
                {
                    Throw.CKException( $"Invalid type '{type.ToCSharpName()}': cannot be a IPoco and a IRealObject or IAutoService at the same time." );
                }
            }
            c = isRealObject
                    ? new RealObjectCachedType( this, type, maxDepth + 1, knownAssembly, interfaces, baseType )
                    : isPoco
                        ? new PocoCachedType( this, type, maxDepth + 1, knownAssembly, interfaces, baseType )
                        : isValueType
                            ? new CachedType( this, type, maxDepth + 1, nullableValueType, knownAssembly, interfaces )
                            : new CachedType( this, type, maxDepth + 1, knownAssembly, interfaces, baseType );
            _types.Add( type, c );
            if( nullableValueType != null )
            {
                _types.Add( nullableValueType, c.Nullable );
            }
        }
        return c;
    }

    public bool ApplyExternalTypesKind( IActivityMonitor monitor, EngineConfiguration configuration, out byte[] hashExternalTypes )
    {
        using var hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
        bool success = true;
        foreach( var eT in configuration.ExternalTypes )
        {
            try
            {
                var cT = Get( eT.Type );
                var msg = GetConfiguredTypeErrorMessage( cT );
                if( msg != null )
                {
                    monitor.Warn( $"Ignoring External type configuration: '{cT.CSharpName}' {msg}." );
                }
                else
                {
                    Throw.DebugAssert( cT is CachedType );
                    ((CachedType)cT).SetKind( (TypeKind)eT.Kind );
                    hasher.Append( cT.CSharpName ).Append( eT.Kind );
                }
            }
            catch( Exception ex )
            {
                monitor.Error( "Unable to apply External type configuration.", ex );
                success = false;
            }
        }
        hashExternalTypes = hasher.GetHashAndReset();
        return success;

        static string? GetConfiguredTypeErrorMessage( ICachedType type )
        {
            var kind = type.Kind;
            var msg = kind.GetUnhandledMessage();
            if( msg == null )
            {
                string? k = null;
                if( (kind & TypeKind.IsAutoService) != 0 )
                {
                    k = nameof( IAutoService );
                }
                else if( (kind & TypeKind.IsRealObject) != 0 )
                {
                    k = nameof( IRealObject );
                }
                else if( (kind & TypeKind.IsPoco) != 0 )
                {
                    k = nameof( IPoco );
                }
                if( k != null )
                {
                    return $"is a {k}. IAutoService, IRealObject and IPoco cannot be externally configured";
                }
            }
            return msg;
        }
    }


}

