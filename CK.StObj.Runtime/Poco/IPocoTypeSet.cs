using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Immutable type set, factory of new sub or super sets.
    /// <para>
    /// These sets are always coherent. There are 11 rules:
    /// <list type="number">
    ///     <item>Nullable &lt;=&gt; Non Nullable (this why only non nullable types need to be exposed by a set)</item>
    ///     <item>Any type =&gt; its <see cref="IPocoType.ObliviousType"/></item>
    ///     <item>A <see cref="IUnionPocoType"/> =&gt; at least one of its <see cref="IOneOfPocoType.AllowedTypes"/></item>
    ///     <item>A IReadOnlyList/Set/Dictionary =&gt; its <see cref="ICollectionPocoType.MutableCollection"/></item>
    ///     <item>Any collection =&gt; all its <see cref="ICollectionPocoType.ItemTypes"/></item>
    ///     <item>An enum type =&gt; its <see cref="IEnumPocoType.UnderlyingType"/></item>
    ///     <item>A generic <see cref="IAbstractPocoType"/> =&gt; all its <see cref="IAbstractPocoType.GenericArguments"/></item>
    ///     <item>A <see cref="IAbstractPocoType"/> =&gt; at least one <see cref="IPrimaryPocoType"/> implements it</item>
    ///     <item>A <see cref="ISecondaryPocoType"/> &lt;=&gt; <see cref="IPrimaryPocoType"/></item>
    ///     <item>A <see cref="IPrimaryPocoType"/> =&gt; all its <see cref="IAbstractPocoType"/></item>
    ///     <item>A <see cref="PocoTypeKind.AnonymousRecord"/> =&gt; at least one of its <see cref="ICompositePocoType.Fields"/>' type</item>
    /// </list>
    /// If <see cref="AllowEmptyPocos"/> (or <see cref="AllowEmptyRecords"/>) is false, then the same "at least one field" rule as the AnynymousRecord applies
    /// to <see cref="IPrimaryPocoType"/> (or named <see cref="IRecordPocoType"/>).
    /// </para>
    /// </summary>
    public interface IPocoTypeSet
    {
        /// <summary>
        /// Gets the type system.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets whether the given type is allowed.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the type is contained in this set, false otherwise.</returns>
        bool Contains( IPocoType t );

        /// <summary>
        /// Gets the set of non nullable types.
        /// </summary>
        IReadOnlyCollection<IPocoType> NonNullableTypes { get; }

        /// <summary>
        /// Gets whether empty named records are automatically excluded
        /// (an anonymous records that has all its field's type excluded is automatically excluded).
        /// </summary>
        bool AllowEmptyRecords { get; }

        /// <summary>
        /// Gets whether en empty <see cref="IPrimaryPocoType"/> is automatically excluded.
        /// </summary>
        bool AllowEmptyPocos { get; }

        /// <summary>
        /// Gets whether when including new types, collections of these types are automatically included.
        /// This is almost always true.
        /// To work with sets that don't have this behavior, use <see cref="IPocoTypeSetManager.CreateAll(bool, System.Func{IPocoType, bool})"/>
        /// or <see cref="IPocoTypeSetManager.CreateNone(bool, bool, bool, System.Func{IPocoType, bool})"/>.
        /// </summary>
        bool AutoIncludeCollections { get; }

        /// <summary>
        /// Gets whether this set as the same content of the <paramref name="other"/> one regardless
        /// of its configuration.
        /// </summary>
        /// <param name="other">The other set.</param>
        /// <returns>True if sets are the same set of types.</returns>
        bool SameContentAs( IPocoTypeSet other );

        /// <summary>
        /// Creates a super set of this set.
        /// </summary>
        /// <param name="types">A set of types that must be included.</param>
        /// <param name="withAbstractReadOnlyFieldTypes">
        /// True to consider the <see cref="IPrimaryPocoType"/> fields where <see cref="IPrimaryPocoField.FieldAccess"/>
        /// is <see cref="PocoFieldAccessKind.AbstractReadOnly"/>.
        /// <para>
        /// By default they are skipped. There are very few scenario where including these fields' type makes sense.
        /// </para>
        /// </param>
        /// <returns>A new super set (or this if nothing changed).</returns>
        IPocoTypeSet Include( IEnumerable<IPocoType> types, bool withAbstractReadOnlyFieldTypes = false );

        /// <summary>
        /// Creates a sub set of this set.
        /// </summary>
        /// <param name="types">A set of types that must be excluded.</param>
        /// <returns>A new sub set (or this if nothing changed).</returns>
        IPocoTypeSet Exclude( IEnumerable<IPocoType> types );

        /// <summary>
        /// Excludes named records that have all their field's type excluded.
        /// The returned set has <see cref="AllowEmptyRecords"/> set to false and will not be able to
        /// contain such empty records anymore.
        /// <para>
        /// Anonymous records with no more included field's types are automatically excluded (an empty value tuple is invalid).
        /// </para>
        /// </summary>
        /// <returns>A new sub set (or this if nothing changed).</returns>
        IPocoTypeSet ExcludeEmptyRecords();

        /// <summary>
        /// Excludes <see cref="IPrimaryPocoType"/> that have all their field's type excluded.
        /// The returned set has <see cref="AllowEmptyPocos"/> set to false and will not be able to
        /// contain such empty poco anymore.
        /// </summary>
        /// <returns>A new sub set (or this if nothing changed).</returns>
        IPocoTypeSet ExcludeEmptyPocos();

        /// <summary>
        /// One pass <see cref="ExcludeEmptyRecords"/> and <see cref="ExcludeEmptyPocos"/>.
        /// The returned set has both <see cref="AllowEmptyRecords"/> and <see cref="AllowEmptyPocos"/> set to
        /// false and will not be able to contain any composite with no fields.
        /// </summary>
        /// <returns>A new sub set (or this if nothing changed).</returns>
        IPocoTypeSet ExcludeEmptyRecordsAndPocos();
    }
}
