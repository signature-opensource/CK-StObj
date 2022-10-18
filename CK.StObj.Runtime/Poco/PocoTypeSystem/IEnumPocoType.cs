namespace CK.Setup
{
    /// <summary>
    /// Type for <see cref="PocoTypeKind.Enum"/>.
    /// </summary>
    public interface IEnumPocoType : IPocoType
    {
        /// <summary>
        /// Gets the underlying enumeration type.
        /// This is the nullable integral type if <see cref="IPocoType.IsNullable"/> is true.
        /// </summary>
        IPocoType UnderlyingType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IEnumPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IEnumPocoType NonNullable { get; }

    }
}
