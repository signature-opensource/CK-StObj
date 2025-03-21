using System;
using System.Collections.Generic;

namespace CK.Setup;

/// <summary>
/// The Poco type system manages by default all the types reachable from IPoco
/// objects and explictly registered types in a <see cref="IPocoTypeSystemBuilder"/>.
/// </summary>
public interface IPocoTypeSystem
{
    /// <summary>
    /// Gets the low level Poco directory on which this Type System is built.
    /// </summary>
    IPocoDirectory PocoDirectory { get; }

    /// <summary>
    /// Gets all the registered types by their <see cref="IPocoType.Index"/>.
    /// This contains both nullable and non nullable types.
    /// </summary>
    IReadOnlyList<IPocoType> AllTypes { get; }

    /// <summary>
    /// Gets all the registered non nullable types, including the <see cref="IAbstractPocoType"/> that have
    /// no <see cref="IPrimaryPocoType"/> implementations.
    /// <para>
    /// The <see cref="SetManager"/>'s <see cref="IPocoTypeSetManager.All"/> contains only the abstract Poco that
    /// have at least one implementation. 
    /// </para>
    /// </summary>
    IReadOnlyList<IPocoType> AllNonNullableTypes { get; }

    /// <summary>
    /// Gets the set of types that are <see cref="IPocoType.IsFinalType"/>.
    /// </summary>
    IReadOnlyCollection<IPocoType> NonNullableFinalTypes { get; }

    /// <summary>
    /// Tries to find by type. Only types that are oblivious (see <see cref="IPocoType.ObliviousType"/>) and IPoco
    /// interfaces can be found by this method.
    /// </summary>
    /// <param name="type">The type to find.</param>
    /// <returns>The Poco type or null.</returns>
    IPocoType? FindByType( Type type );

    /// <summary>
    /// Tries to find by type. Only types that are oblivious (see <see cref="IPocoType.ObliviousType"/>) and IPoco
    /// interfaces can be found by this method.
    /// </summary>
    /// <typeparam name="T">The expected <see cref="IPocoType"/>.</typeparam>
    /// <param name="type">The type to find.</param>
    /// <returns>The Poco type or null.</returns>
    T? FindByType<T>( Type type ) where T : class, IPocoType;

    /// <summary>
    /// Finds an open generic definition of <see cref="IAbstractPocoType"/>.
    /// The type must be used by at least one <see cref="IPocoGenericTypeDefinition.Instances"/>.
    /// </summary>
    /// <param name="type">Type to find. Must be an open generic type (like <c>typeof( ICommand&lt;&gt; )</c>).</param>
    /// <returns>The type definition or null.</returns>
    IPocoGenericTypeDefinition? FindGenericTypeDefinition( Type type );

    /// <summary>
    /// Gets the type set manager.
    /// </summary>
    IPocoTypeSetManager SetManager { get; }
}
