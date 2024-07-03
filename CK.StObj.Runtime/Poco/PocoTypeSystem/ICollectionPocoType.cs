using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
        [MemberNotNullWhen( false, nameof( ConcreteCollection ), nameof( RegularType ), nameof( StructuralFinalType ), nameof( NonSecondaryConcreteCollection ) )]
        bool IsAbstractReadOnly { get; }

        /// <summary>
        /// Gets the generic arguments or this collection.
        /// </summary>
        IReadOnlyList<IPocoType> ItemTypes { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType" />
        new ICollectionPocoType ObliviousType { get; }

        /// <summary>
        /// Gets the associated regular collection: whether <see cref="IsAbstractCollection"/> is true or not,
        /// this is the <see cref="List{T}"/>, <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/>
        /// where the generic parameters are regular types (anonymous records have no field names and subordinated
        /// collections are regular).
        /// <para>
        /// This is never null except when <see cref="IsAbstractReadOnly"/> is true.
        /// </para>
        /// </summary>
        new ICollectionPocoType? RegularType { get; }

        /// <summary>
        /// Gets the concrete <see cref="List{T}"/>, <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/>
        /// collection with the same nullability.
        /// <para>
        /// This is never null except when <see cref="IsAbstractReadOnly"/> is true.
        /// </para>
        /// </summary>
        ICollectionPocoType? ConcreteCollection { get; }

        /// <summary>
        /// Gets the concrete <see cref="List{T}"/>, <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/>
        /// collection with the same nullability where <c>T</c> or <c>TValue</c> is the <see cref="ISecondaryPocoType.PrimaryPocoType"/>
        /// if this <c>T</c> or <c>TValue</c> is a secondary poco.
        /// <para>
        /// This is never null except when <see cref="IsAbstractReadOnly"/> is true.
        /// </para>
        /// </summary>
        ICollectionPocoType? NonSecondaryConcreteCollection { get; }

        /// <inheritdoc cref="IPocoType.StructuralFinalType" />
        /// <remarks>
        /// This is never null except when <see cref="IsAbstractReadOnly"/> is true.
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
