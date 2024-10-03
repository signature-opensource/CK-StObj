using System.Collections.Generic;

namespace CK.Setup;

/// <summary>
/// Unifies <see cref="IAbstractPocoType"/> and <see cref="ICompositePocoType"/> (that itself
/// unifies <see cref="IPrimaryPocoType"/> and <see cref="IRecordPocoType"/>).
/// </summary>
public interface IBaseCompositeType : IPocoType
{
    /// <summary>
    /// Gets the list of fields.
    /// </summary>
    IReadOnlyList<IBasePocoField> Fields { get; }
}
