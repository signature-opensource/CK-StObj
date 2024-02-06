namespace CK.Setup
{
    /// <summary>
    /// Defines any type to name association. <see cref="PocoTypeNameMap"/> implements
    /// a standard naming and can be specialized.
    /// </summary>
    public interface IPocoTypeNameMap
    {
        /// <summary>
        /// Gets the set of types that are handled by this map.
        /// </summary>
        IPocoTypeSet TypeSet { get; }

        /// <summary>
        /// Gets the underlying type system.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets a name for a type.
        /// When nullable the name is usually suffixed with "?"
        /// but this is not required. Name is usually also unique.
        /// <para>
        /// What should be enforced by the implementations is that the type must be an element of
        /// the <see cref="TypeSet"/> otherwise an <see cref="ArgumentException"/> must be thrown.
        /// </para>
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The type name.</returns>
        string GetName( IPocoType type );
    }
}
