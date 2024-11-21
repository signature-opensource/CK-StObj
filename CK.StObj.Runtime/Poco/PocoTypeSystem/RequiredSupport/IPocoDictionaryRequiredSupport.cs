namespace CK.Setup;

/// <summary>
/// Defines the "multi variance" dictionary for a Poco.
/// </summary>
public interface IPocoDictionaryRequiredSupport : IPocoRequiredSupportType
{
    /// <summary>
    /// Gets the necessary non nullable, <see cref="IPocoType.IsReadOnlyCompliant"/> and not <see cref="IPocoType.IsPolymorphic"/> key type.
    /// </summary>
    IPocoType KeyType { get; }

    /// <summary>
    /// Gets the oblivious (nullable) type of the value.
    /// </summary>
    IPrimaryPocoType ValueType { get; }
}
