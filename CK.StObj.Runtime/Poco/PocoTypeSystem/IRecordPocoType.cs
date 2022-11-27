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

        /// <inheritdoc cref="ICompositePocoType.Fields"/>
        new IReadOnlyList<IRecordPocoField> Fields { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType"/>
        new IRecordPocoType ObliviousType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IRecordPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IRecordPocoType NonNullable { get; }

    }
}
