namespace CK.Setup
{
    /// <summary>
    /// Type for <see cref="PocoTypeKind.Enum"/>.
    /// </summary>
    public interface IEnumPocoType : IPocoType, INamedPocoType
    {
        /// <summary>
        /// Gets the underlying enumeration type.
        /// This is the nullable integral type if <see cref="IPocoType.IsNullable"/> is true.
        /// </summary>
        IPocoType UnderlyingType { get; }

        /// <summary>
        /// Gets the default value enumeration name.
        /// It corresponds to the smallest unsigned numerical value: it is usually the name with the 0 value.
        /// </summary>
        string? DefaultValueName { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IEnumPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IEnumPocoType NonNullable { get; }

    }
}
