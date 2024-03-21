namespace CK.Setup
{
    /// <summary>
    /// A <see cref="PocoTypeKind.AnonymousRecord"/> has its associated <see cref="UnnamedRecord"/>.
    /// </summary>
    public interface IAnonymousRecordPocoType : IRecordPocoType
    {
        /// <summary>
        /// Gets whether all fields are <see cref="IRecordPocoField.IsUnnamed"/>.
        /// </summary>
        bool IsUnnamed { get; }

        /// <summary>
        /// Gets the anonymous record where all fields are <see cref="IRecordPocoField.IsUnnamed"/>.
        /// This if <see cref="IsUnnamed"/> is true.
        /// </summary>
        IAnonymousRecordPocoType UnnamedRecord { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType"/>
        new IAnonymousRecordPocoType ObliviousType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IAnonymousRecordPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IAnonymousRecordPocoType NonNullable { get; }

    }
}
