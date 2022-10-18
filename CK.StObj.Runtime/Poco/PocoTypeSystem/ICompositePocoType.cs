using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Common type of <see cref="IConcretePocoType"/> and <see cref="IRecordPocoType"/>.
    /// </summary>
    public interface ICompositePocoType : IPocoType
    {
        /// <summary>
        /// Gets the list of fields.
        /// </summary>
        IReadOnlyList<IPocoField> Fields { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new ICompositePocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new ICompositePocoType NonNullable { get; }
    }
}
