using System.Collections.Generic;
using static CK.Setup.IPocoType;

namespace CK.Setup
{
    /// <summary>
    /// Collection type is <see cref="PocoTypeKind.Array"/>, <see cref="PocoTypeKind.HashSet"/>,
    /// <see cref="PocoTypeKind.Dictionary"/> or <see cref="PocoTypeKind.List"/>.
    /// </summary>
    public interface ICollectionPocoType : IPocoType
    {
        /// <summary>
        /// Gets whether this is a <see cref="IList{T}"/>, <see cref="ISet{T}"/>, <see cref="IDictionary{TKey, TValue}"/>
        /// or a <see cref="IReadOnlyList{T}"/>, <see cref="IReadOnlySet{T}"/>, <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
        /// </summary>
        bool IsAbstractCollection { get; }

        /// <summary>
        /// Gets whether this is a <see cref="IReadOnlyList{T}"/>, <see cref="IReadOnlySet{T}"/> or <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
        /// <para>
        /// <see cref="IsAbstractCollection"/> is true and <see cref="MutableCollection"/> is not this instance.
        /// Such type can only appear in IPoco fields.
        /// </para>
        /// </summary>
        bool IsAbstractReadOnly { get; }

        /// <summary>
        /// Gets the mutable collection type (the <see cref="IList{T}"/>, <see cref="ISet{T}"/> or <see cref="IDictionary{TKey, TValue}"/>).
        /// <para>
        /// This is this collection if this collection is mutable.
        /// </para>
        /// </summary>
        ICollectionPocoType MutableCollection { get; }

        /// <summary>
        /// Gets the generic parameters or the array element type.
        /// </summary>
        IReadOnlyList<IPocoType> ItemTypes { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType" />
        new ICollectionPocoType ObliviousType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new ICollectionPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new ICollectionPocoType NonNullable { get; }

    }
}
