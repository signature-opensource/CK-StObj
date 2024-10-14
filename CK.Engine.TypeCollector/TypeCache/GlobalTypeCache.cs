using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Global type cache.
/// </summary>
public sealed partial class GlobalTypeCache
{
    readonly Dictionary<Type, CachedType> _types;
    readonly AssemblyCache _assemblies;
    readonly ICachedType _iRealObject;
    readonly ICachedType _iPoco;

    /// <summary>
    /// Initializes a new cahe for types based on an assembly cache.
    /// </summary>
    /// <param name="assemblies">The assembly cache.</param>
    public GlobalTypeCache( AssemblyCache assemblies )
    {
        _types = new Dictionary<Type, CachedType>();
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

    internal ICachedType Get( Type type, CachedAssembly? knwonAssembly )
    {
        Throw.CheckArgument( type is not null
                             && type.IsByRef is false );
        if( !_types.TryGetValue( type, out CachedType? c ) )
        {
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
                    if( !type.IsByRefLike )
                    {
                        nullableValueType = typeof( Nullable<> ).MakeGenericType( type );
                    }
                }
            }
            // Only then can we work on the type.
            ICachedType? genericTypeDefinition = type.IsGenericType && !type.IsGenericTypeDefinition
                                                    ? Get( type.GetGenericTypeDefinition() )
                                                    : null;
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
            // No check of a IRealObject that would also be a IPoco here:
            // if this weird case happens, this is handled by upper layers.
            knwonAssembly ??= _assemblies.FindOrCreate( type.Assembly );
            c = interfaces.Contains( _iRealObject )
                    ? new RealObjectCachedType( this, type, maxDepth + 1, nullableValueType, knwonAssembly, interfaces, baseType, genericTypeDefinition )
                    : interfaces.Contains( _iPoco )
                        ? new PocoCachedType( this, type, maxDepth + 1, nullableValueType, knwonAssembly, interfaces, baseType, genericTypeDefinition )
                        : new CachedType( this, type, maxDepth + 1, nullableValueType, knwonAssembly, interfaces, baseType, genericTypeDefinition );
            _types.Add( type, c );
        }
        return c;
    }
}
