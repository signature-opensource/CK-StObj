namespace CK.Setup
{
    /// <summary>
    /// Poco extension interface. Corresponds to the <see cref="IPocoFamilyInfo.Interfaces"/>
    /// except the first one that is the <see cref="IPrimaryPocoType"/>.
    /// </summary>
    public interface ISecondaryPocoType : IPocoType
    {
        /// <inheritdoc cref="IPocoType.Nullable" />
        new ISecondaryPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new ISecondaryPocoType NonNullable { get; }

        /// <summary>
        /// Gets the <see cref="IPrimaryPocoType"/> with the same nullability.
        /// </summary>
        IPrimaryPocoType PrimaryPocoType { get; }

        /// <summary>
        /// Gets the non nullable <see cref="IPrimaryPocoType"/>.
        /// </summary>
        new IPrimaryPocoType ObliviousType { get; }
    }

}
