using System;
using System.Collections.Generic;

namespace CK.Setup;

/// <summary>
/// Immutable type set, factory of new sub or super sets.
/// A set never contains an implementation less poco: <see cref="IPocoType.ImplementationLess"/>
/// is always false.
/// <para>
/// These sets are always coherent. There are 12 rules:
/// <list type="number">
///     <item>Nullable &lt;=&gt; Non Nullable (this why only non nullable types need to be exposed by a set)</item>
///     <item>Any type =&gt; its <see cref="IPocoType.ObliviousType"/></item>
///     <item>A <see cref="IUnionPocoType"/> =&gt; at least one of its <see cref="IOneOfPocoType.AllowedTypes"/></item>
///     <item>A <see cref="IBasicRefPocoType"/> =&gt; all its <see cref="IBasicRefPocoType.BaseTypes"/>.</item>
///     <item>Any type =&gt; its <see cref="IPocoType.RegularType"/>.</item>
///     <item>Any collection =&gt; all its <see cref="ICollectionPocoType.ItemTypes"/></item>
///     <item>An enum type =&gt; its <see cref="IEnumPocoType.UnderlyingType"/></item>
///     <item>A generic <see cref="IAbstractPocoType"/> =&gt; all its <see cref="IAbstractPocoType.GenericArguments"/></item>
///     <item>A <see cref="IAbstractPocoType"/> =&gt; at least one <see cref="IPrimaryPocoType"/> implements it</item>
///     <item>A <see cref="ISecondaryPocoType"/> &lt;=&gt; <see cref="IPrimaryPocoType"/></item>
///     <item>A <see cref="IPrimaryPocoType"/> =&gt; all its <see cref="IPrimaryPocoType.AbstractTypes"/></item>
///     <item>A <see cref="PocoTypeKind.AnonymousRecord"/> =&gt; at least one of its <see cref="ICompositePocoType.Fields"/>' type</item>
/// </list>
/// If <see cref="AllowEmptyPocos"/> (or <see cref="AllowEmptyRecords"/>) is false, then the same "at least one field" rule as the AnoynymousRecord applies
/// to <see cref="IPrimaryPocoType"/> (or named <see cref="IRecordPocoType"/>).
/// </para>
/// </summary>
public interface IPocoTypeSet : IReadOnlyPocoTypeSet
{
    /// <summary>
    /// Gets the type system.
    /// </summary>
    IPocoTypeSystem TypeSystem { get; }

    /// <summary>
    /// Gets the set of non nullable types.
    /// </summary>
    IReadOnlyPocoTypeSet NonNullableTypes { get; }

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
    /// To work with sets that don't have this behavior, use <see cref="IPocoTypeSetManager.CreateAll(bool, bool, bool, Func{IPocoType, bool})"/>
    /// or <see cref="IPocoTypeSetManager.CreateNone(bool, bool, bool, Func{IPocoType, bool})"/>.
    /// </summary>
    bool AutoIncludeCollections { get; }

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
    /// Combines includes and excludes in a single resolution. Note that a type can be in both set:
    /// while including it, its dependent types will also be included and then the type will be excluded but
    /// the dependent types that don't require it will remain in the set.
    /// <para>
    /// Fields where <see cref="IPrimaryPocoField.FieldAccess"/> is <see cref="PocoFieldAccessKind.AbstractReadOnly"/> are skipped.
    /// </para>
    /// </summary>
    /// <param name="include">The types to include in the set.</param>
    /// <param name="exclude">The types to exclude.</param>
    /// <returns>A new set (or this if nothing changed).</returns>
    IPocoTypeSet IncludeAndExclude( IEnumerable<IPocoType> include, IEnumerable<IPocoType> exclude );

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

    /// <summary>
    /// Gets whether this set as the same content of the <paramref name="other"/> one regardless
    /// of its configuration.
    /// </summary>
    /// <param name="other">The other set.</param>
    /// <returns>True if sets contains the same types.</returns>
    bool SameContentAs( IPocoTypeSet other );

    /// <summary>
    /// Gets whether this set is a super set of another set.
    /// <para>
    /// The other <see cref="TypeSystem"/> must be the same as this one otherwise
    /// an <see cref="ArgumentException"/> is thrown.
    /// </para>
    /// </summary>
    /// <param name="other">The other type set.</param>
    /// <returns>True if this set is a super set of the other one.</returns>
    bool IsSupersetOf( IPocoTypeSet other );

    /// <summary>
    /// Gets the internal array of flags that describes this set.
    /// This supports the code generation, this is barely usable for any other operations.
    /// </summary>
    /// <returns>An array of flags.</returns>
    IReadOnlyList<int> FlagArray { get; }
}
