namespace CK.Setup;

/// <summary>
/// Defines the "multi variance" dictionary for AbstractPoco values.
/// </summary>
public interface IPocoDictionaryOfAbstractOrBasicRefRequiredSupport : IPocoRequiredSupportType
{
    /// <summary>
    /// Gets the necessary non nullable, <see cref="IPocoType.IsReadOnlyCompliant"/> and not <see cref="IPocoType.IsPolymorphic"/> key type.
    /// </summary>
    IPocoType KeyType { get; }

    /// <summary>
    /// Gets the oblivious (nullable) item type: a <see cref="IBasicRefPocoType"/> or <see cref="IAbstractPocoType"/>.
    /// </summary>
    IPocoType ValueType { get; }
}
