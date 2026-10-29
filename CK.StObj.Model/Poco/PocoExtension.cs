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
        if( c == null ) Throw.Exception( $"Unable to resolve concrete IPoco interface '{typeof( T ).ToCSharpName()}' from PocoDirectory." );
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

    /// <summary>
    /// Creates a deep copy of this Poco. See <see cref="IPocoGeneratedClass.Clone()"/>.
    /// </summary>
    /// <typeparam name="T">The type of the poco.</typeparam>
    /// <param name="this">This poco.</param>
    /// <returns>A new instance.</returns>
    public static T Clone<T>( this T @this ) where T : class, IPoco
    {
        Throw.CheckNotNullArgument( @this );
        return (T)((IPocoGeneratedClass)@this).Clone();
    }

    /// <summary>
    /// Creates a new <typeparamref name="T"/> from this Poco: "any Poco is a factory of any other Poco".
    /// When this Poco is a <typeparamref name="T"/>, this is a <see cref="Clone{T}(T)"/>, otherwise the properties of the
    /// abstract Pocos both types implement are copied. See <see cref="IPocoFactory.CreateFrom(IPoco)"/>.
    /// </summary>
    /// <typeparam name="T">The type of the poco to create. Must not be an abstract Poco.</typeparam>
    /// <param name="this">This poco.</param>
    /// <returns>A new instance.</returns>
    public static T Create<T>( this IPoco @this ) where T : class, IPoco
    {
        Throw.CheckNotNullArgument( @this );
        var f = ((IPocoGeneratedClass)@this).Factory.PocoDirectory.Find( typeof( T ) );
        if( f == null ) Throw.InvalidOperationException( $"Unable to resolve concrete IPoco interface '{typeof( T ).ToCSharpName()}' from PocoDirectory." );
        return (T)f.CreateFrom( @this );
    }

    /// <summary>
    /// Creates and configures a new <typeparamref name="T"/> from this Poco. See <see cref="Create{T}(IPoco)"/>.
    /// </summary>
    /// <typeparam name="T">The type of the poco to create. Must not be an abstract Poco.</typeparam>
    /// <param name="this">This poco.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>A new configured instance.</returns>
    public static T Create<T>( this IPoco @this, Action<T> configure ) where T : class, IPoco
    {
        var p = @this.Create<T>();
        configure( p );
        return p;
    }
}
