namespace CK.Setup
{
    /// <summary>
    /// Associates a <typeparamref name="T"/> for each <see cref="IPocoType"/>
    /// of a type system.
    /// <para>
    /// This can be implemented in a multiple of ways but this should never be considered
    /// thread safe. Typical implementations rely on a array indexed by <see cref="IPocoType.Index"/>.
    /// Whether the associated values are precomputed or computed on demand is implementation dependent.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The associated type.</typeparam>
    public interface IPocoTypeMap<T>
    {
        /// <summary>
        /// Gets the type system.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets the associated value.
        /// </summary>
        /// <param name="t">The poco type.</param>
        /// <returns>The value.</returns>
        T this[IPocoType t] { get; }

        /// <summary>
        /// Gets the associated value.
        /// </summary>
        /// <param name="t">The poco type.</param>
        /// <returns>The value.</returns>
        T Get( IPocoType t );
    }

}
