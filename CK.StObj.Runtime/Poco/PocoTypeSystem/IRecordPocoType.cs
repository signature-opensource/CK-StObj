using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Type for <see cref="PocoTypeKind.Record"/> and <see cref="PocoTypeKind.AnonymousRecord"/>.
    /// </summary>
    public interface IRecordPocoType : ICompositePocoType
    {
        /// <summary>
        /// Gets whether this is a value tuple that defines this record.
        /// </summary>
        bool IsAnonymous { get; }

        /// <summary>
        /// Gets whether this record must be initialized in the constructor of its owner.
        /// It is true if a field has a <see cref="IPocoField.DefaultValue"/> (it must be
        /// assigned) or is a non nullable reference type (it must be instantiated).
        /// </summary>
        bool RequiresInit { get; }

        /// <summary>
        /// Gets the list of fields.
        /// </summary>
        new IReadOnlyList<IRecordPocoField> Fields { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IRecordPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IRecordPocoType NonNullable { get; }

    }
}
