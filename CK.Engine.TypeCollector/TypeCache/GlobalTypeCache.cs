using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

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

    /// <summary>
    /// Initializes a new cahe for types based on an assembly cache.
    /// </summary>
    /// <param name="assemblies">The assembly cache.</param>
    public GlobalTypeCache( AssemblyCache assemblies )
    {
        _types = new Dictionary<Type, ICachedType>();
        _assemblies = assemblies;
        _iRealObject = Get( typeof( IRealObject ) );
        _iPoco = Get( typeof( IPoco ) );
    }

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
                        && type != typeof(void)
                        && type != typeof(Nullable<>) )
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
            var isRealObject = interfaces.Contains( _iRealObject );
            var isPoco = interfaces.Contains( _iPoco );
            if( isRealObject || isPoco )
            {
                if( isRealObject && isPoco )
                {
                    Throw.CKException( $"Invalid type '{type}': cannot be a IPoco and a IRealObject at the same time." );
                }
                if( !type.IsClass && !type.IsInterface )
                {
                    Throw.CKException( $"Invalid type '{type}': cannot be a IPoco or a IRealObject. It must be a class or an interface." );
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
}
