using System.Collections.Generic;

namespace CK.Setup;

/// <summary>
/// Qualifies the <see cref="IPrimaryPocoField.FieldAccess"/>.
/// </summary>
public enum PocoFieldAccessKind
{
    /// <summary>
    /// The field is a <c>{ get; }</c> only property: there is no interface in the family that actually defines this field
    /// with a setter that can provide a value for it.
    /// <para>
    /// Such fields should remain "hidden" (typically not exchangeable) and are de facto useless (because they'll keep their initial default
    /// value forever) until an interface in the family that defines it with a setter appear in the compiled project.
    /// </para>
    /// Such field type typically is:
    /// <list type="bullet">
    ///     <item><c>object?</c> as the ultimate representation of anything.</item>
    ///     <item>A <see cref="PocoTypeKind.AbstractPoco"/> (also nullable) as the specification of a set of possible IPoco families.</item>
    ///     <item>
    ///     A collection that is a <see cref="ICollectionPocoType.IsAbstractReadOnly"/> (that is the specification of any <see cref="IList{T}"/>, <see cref="ISet{T}"/>
    ///     or <see cref="IReadOnlyDictionary{TKey, TValue}"/> where T or TValue are covariant with the readonly collection type).
    ///     </item>
    /// </list>
    /// There is no point to allow records to be Abstract Read Only properties for 2 reasons:
    /// <list type="number">
    ///     <item>the type is completely defined, there is no possible "specialization".</item>
    ///     <item>the "ref" can easily be forgotten by the developper, preventing the easy update of the value.</item>
    /// </list>
    /// <para>
    /// One can notice that the type is also completely defined for basic types (value types or immutable reference types),
    /// for primary/secondary poco and even for array (because array are strictly invariant)... but for them, there's
    /// little to no risk to miss the 'ref' access (that is invalid by the way - ref applies only to records),
    /// so we allow a <c>string Name { get; }</c> or <c>int[] Values { get; }</c> to exist as a kind of "optional property definition".
    /// </para>
    /// </summary>
    AbstractReadOnly,

    /// <summary>
    /// The field is a regular <c>{ get; set; }</c> property.
    /// Its type can be:
    /// <list type="bullet">
    /// <item>An array or a concrete <see cref="List{T}"/>, <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/> collection.</item>
    /// <item>A <see cref="IPrimaryPocoType"/> (or <see cref="ISecondaryPocoType"/>).</item>
    /// <item>A basic type, including object (Any).</item>
    /// </list>
    /// When not nullable, a value type is initialized (with its [DefaultValue] for basic types if it exists) and a reference
    /// type is initially allocated.
    /// <para>
    /// This cannot be a record (named or anonymous): records are necessarily <see cref="IsByRef"/>.
    /// </para>
    /// </summary>
    HasSetter,

    /// <summary>
    /// The field is a ref property. Its type is a <see cref="IRecordPocoType"/> (anonymous or not).
    /// </summary>
    IsByRef,

    /// <summary>
    /// The field has no setter and is a <see cref="ICollectionPocoType"/> (a list, set
    /// or dictionary but not an array) or a <see cref="IPrimaryPocoType"/> or a <see cref="ISecondaryPocoType"/>.
    /// </summary>
    MutableReference
}
