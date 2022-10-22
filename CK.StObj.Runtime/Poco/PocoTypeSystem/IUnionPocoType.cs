namespace CK.Setup
{
    /// <summary>
    /// Union type of Poco types.
    /// </summary>
    public interface IUnionPocoType : IUnionPocoType<IPocoType>
    {
        /// <inheritdoc cref="IPocoType.Nullable" />
        new IUnionPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IUnionPocoType NonNullable { get; }
    }

}
