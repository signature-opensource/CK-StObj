namespace CK.Setup;

/// <summary>
/// A field of a <see cref="IRecordPocoType"/> can have no name
/// when its owner's <see cref="IRecordPocoType.IsAnonymous"/> is true.
/// </summary>
public interface IRecordPocoField : IPocoField
{
    /// <summary>
    /// Gets whether this field has no real name: it is <c>Item{Index+1}</c>.
    /// <para>
    /// This can be true only for anonymous records (value tuples).
    /// </para>
    /// </summary>
    bool IsUnnamed { get; }
}
