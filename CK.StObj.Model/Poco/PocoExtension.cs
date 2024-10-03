using System;

namespace CK.Core;

/// <summary>
/// Extends <see cref="IPoco"/> objects.
/// </summary>
public static class PocoExtension
{
    /// <summary>
    /// Creates and configures a <see cref="IPoco"/> instance.
    /// </summary>
    /// <typeparam name="T">Type of the poco.</typeparam>
    /// <param name="this">This poco factory.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The configured instance.</returns>
    public static T Create<T>( this IPocoFactory<T> @this, Action<T> configure ) where T : IPoco
    {
        T p = @this.Create();
        configure( p );
        return p;
    }

    /// <summary>
    /// Creates a new <see cref="IPoco"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of the poco.</typeparam>
    /// <param name="this">This directory.</param>
    /// <returns>A new instance.</returns>
    public static T Create<T>( this PocoDirectory @this ) where T : IPoco
    {
        var c = @this.Find( typeof( T ) );
        if( c == null ) Throw.Exception( $"Unable to resolve concrete IPoco interface '{typeof(T).ToCSharpName()}' from PocoDirectory." );
        return (T)c.Create();
    }

    /// <summary>
    /// Creates and configures a <see cref="IPoco"/> instance.
    /// </summary>
    /// <typeparam name="T">The type of the poco.</typeparam>
    /// <param name="this">This directory.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>A new configured instance.</returns>
    public static T Create<T>( this PocoDirectory @this, Action<T> configure ) where T : IPoco
    {
        var p = @this.Create<T>();
        configure( p );
        return p;
    }

}
