using System.Collections.Generic;

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
        /// <para>
        /// Such type can only appear in IPoco fields.
        /// </para>
        /// </summary>
        bool IsAbstractCollection { get; }

        /// <summary>
        /// Gets whether this is a <see cref="IReadOnlyList{T}"/>, <see cref="IReadOnlySet{T}"/> or <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
        /// <para>
        /// <see cref="IsAbstractCollection"/> is true. Such type can only appear in IPoco fields.
        /// </para>
        /// </summary>
        bool IsAbstractReadOnly { get; }

        /// <summary>
        /// Gets the generic arguments or this collection.
        /// </summary>
        IReadOnlyList<IPocoType> ItemTypes { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType" />
        new ICollectionPocoType ObliviousType { get; }

        /// <summary>
        /// If <see cref="IsAbstractReadOnly"/> is true, this is null.
        /// <para>
        /// Gets the associated regular collection: whether <see cref="IsAbstractCollection"/> is true or not,
        /// this is the <see cref="List{T}"/>, <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/>
        /// where the generic parameters are regular types (anonymous records have no field names and subordinated
        /// collections are regular).
        /// </para>
        /// </summary>
        new ICollectionPocoType? RegularType { get; }

        /// <inheritdoc cref="IPocoType.StructuralFinalType" />
        /// <remarks>
        /// This is null when <see cref="IsAbstractReadOnly"/> is true.
        /// </remarks>
        new ICollectionPocoType? StructuralFinalType { get; }

        /// <summary>
        /// Gets the final type.
        /// This is null when <see cref="IPocoType.ImplementationLess"/> or <see cref="IsAbstractReadOnly"/> is true.
        /// </summary>
        new ICollectionPocoType? FinalType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new ICollectionPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new ICollectionPocoType NonNullable { get; }

    }
}
