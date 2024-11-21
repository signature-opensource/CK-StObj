namespace CK.Setup;

/// <summary>
/// Defines the "multi variance" set for Poco abstractions and BasicRefType.
/// </summary>
public interface IPocoHashSetOfAbstractOrBasicRefRequiredSupport : IPocoRequiredSupportType
{
    /// <summary>
    /// Gets the non nullable item type: a <see cref="IBasicRefPocoType"/> or <see cref="IAbstractPocoType"/>.
    /// </summary>
    IPocoType ItemType { get; }
}
