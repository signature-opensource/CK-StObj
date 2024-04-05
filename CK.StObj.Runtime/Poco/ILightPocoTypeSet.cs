namespace CK.Setup
{
    /// <summary>
    /// Minimal set of type required by the <see cref="PocoTypeVisitor{T}"/>.
    /// </summary>
    public interface ILightPocoTypeSet
    {
        /// <summary>
        /// Gets whether the given type is contained in this set.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the type is contained, false otherwise.</returns>
        bool Contains( IPocoType t );

        /// <summary>
        /// Adds a type to this set.
        /// </summary>
        /// <param name="t">The type to add.</param>
        /// <returns>True if the type has been added, false if it already exists.</returns>
        bool Add( IPocoType t );

        /// <summary>
        /// Clears this set.
        /// </summary>
        void Clear();
    }
}
