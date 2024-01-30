namespace CK.Setup
{
    /// <summary>
    /// Filter for PocoType. Can only be built by <see cref="PocoTypeFilterBuilder"/>.
    /// <para>
    /// This relies on the <see cref="IPocoType.ITypeRef"/>: when a type is disallowed, all types
    /// that depend on it are automatically disallowed. A <see cref="ICompositePocoType"/>
    /// is disallowed when all its <see cref="ICompositePocoType.Fields"/>' types are disallowed.
    /// </para>
    /// </summary>
    public abstract class PocoTypeFilter
    {
        internal PocoTypeFilter() { }

        /// <summary>
        /// Gets whether the given type is allowed. The <see cref="PocoTypeKind.Any"/>
        /// is necessarily allowed.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the type is allowed, false otherwise.</returns>
        public abstract bool IsAllowed( IPocoType t );

        sealed class All : PocoTypeFilter
        {
            public override bool IsAllowed( IPocoType t ) => true;
        }

        sealed class None : PocoTypeFilter
        {
            public override bool IsAllowed( IPocoType t ) => t.Kind == PocoTypeKind.Any;
        }

        /// <summary>
        /// A filter that allows every types.
        /// </summary>
        public static readonly PocoTypeFilter AllowAll = new All();

        /// <summary>
        /// A filter that disallows every types (except the <see cref="PocoTypeKind.Any"/>).
        /// </summary>
        public static readonly PocoTypeFilter AllowNone = new None();
    }

}
