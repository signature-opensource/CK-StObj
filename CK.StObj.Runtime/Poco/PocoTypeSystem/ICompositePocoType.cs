using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Common type of <see cref="IPrimaryPocoType"/> and <see cref="IRecordPocoType"/>.
    /// A composite has one or more <see cref="IPocoField"/> named and indexed fields.
    /// </summary>
    public interface ICompositePocoType : IPocoType
    {
        /// <summary>
        /// Gets the list of fields.
        /// </summary>
        IReadOnlyList<IPocoField> Fields { get; }

        /// <summary>
        /// Gets an anonymous record type (Value Tuple) that has no field names
        /// but the exact same field types in same order with the exact same default values.
        /// </summary>
        IRecordPocoType NakedRecord { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new ICompositePocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new ICompositePocoType NonNullable { get; }
    }
}
