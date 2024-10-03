namespace CK.Setup;

/// <summary>
/// Defines the "multi variance" list or set for a Poco.
/// </summary>
public interface IPocoListOrHashSetRequiredSupport : IPocoRequiredSupportType
{
    /// <summary>
    /// Gets whether this is a list (or a hash set).
    /// </summary>
    bool IsList { get; }

    /// <summary>
    /// Gets the nullable item type.
    /// </summary>
    IPrimaryPocoType ItemType { get; }
}
