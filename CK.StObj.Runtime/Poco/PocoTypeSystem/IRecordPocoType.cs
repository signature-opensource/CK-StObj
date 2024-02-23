using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Type for <see cref="PocoTypeKind.Record"/> (fully mutable structs)
    /// and <see cref="PocoTypeKind.AnonymousRecord"/>.
    /// </summary>
    public interface IRecordPocoType : ICompositePocoType
    {
        /// <summary>
        /// Gets whether this is a value tuple that defines this record.
        /// </summary>
        bool IsAnonymous { get; }

        /// <summary>
        /// Gets whether this record type has no mutable reference types: a copy of this value
        /// is de facto a "readonly" projection of its source in the sense that it cannot be
        /// used to mutate the source data.
        /// </summary>
        bool IsReadOnlyCompliant { get; }

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
