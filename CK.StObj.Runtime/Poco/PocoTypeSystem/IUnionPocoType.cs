namespace CK.Setup;

/// <summary>
/// Union type of Poco compliant types.
/// </summary>
public interface IUnionPocoType : IOneOfPocoType
{
    /// <inheritdoc cref="IPocoType.Nullable" />
    new IUnionPocoType Nullable { get; }

    /// <inheritdoc cref="IPocoType.NonNullable" />
    new IUnionPocoType NonNullable { get; }
}
