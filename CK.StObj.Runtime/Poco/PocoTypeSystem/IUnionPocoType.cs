namespace CK.Setup
{
    /// <summary>
    /// Union type of Poco compliant types. In .Net this is a simple <c>object</c>.
    /// </summary>
    public interface IUnionPocoType : IAnyOfPocoType<IPocoType>
    {
        /// <inheritdoc cref="IPocoType.Nullable" />
        new IUnionPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IUnionPocoType NonNullable { get; }
    }

}
