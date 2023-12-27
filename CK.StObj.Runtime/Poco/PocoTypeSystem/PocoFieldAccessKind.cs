using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Qualifies the <see cref="IPrimaryPocoField.FieldAccess"/>.
    /// </summary>
    public enum PocoFieldAccessKind
    {
        /// <summary>
        /// The field is a <c>{ get; }</c> only property. Is is not exchangeable. Its type typically is:
        /// <list type="bullet">
        /// <item><c>object</c> as the ultimate representation of anything.</item>
        /// <item>A <see cref="PocoTypeKind.AbstractPoco"/> that can be "implemented" by any number of "concrete" IPoco families.</item>
        /// <item>A collection that is a <see cref="ICollectionPocoType.IsAbstractReadOnly"/> for which the item type can be any base type of the "implemented" collection.</item>
        /// </list>
        /// There is no point to allow records to be Abstract Read Only properties for 2 reasons:
        /// <list type="number">
        ///<item>the type is completely defined, there is no possible "specialization".</item>
        ///<item>the "ref" can easily be forgotten by the developper, preventing the easy update of the value.</item>
        /// </list>
        /// <para>
        /// One can note that the type is also completely defined for basic types (value types or immutable reference types),
        /// for primary/secondary poco and even for array (because array are strictly invariant)... but for them, there's
        /// little to no risk to miss the 'ref' access (that is invalid by the way - ref is allowed and required only for records),
        /// so we allow a <c>string Name { get; }</c> or <c>int[] Values { get; }</c> to exist as a kind of "optional property definition"
        /// that will remain "hidden" (because not exchangeable) and useless (because they'll keep their initial default value forever)
        /// until another interface family defines it with a setter.
        /// </para>
        /// </summary>
        AbstractReadOnly,

        /// <summary>
        /// The field is a regular <c>{ get; set; }</c> property.
        /// Its type can be:
        /// <list type="bullet">
        /// <item>An array but not any other kind of collection.</item>
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
        /// or dictionary but not an array).
        /// </summary>
        MutableCollection
    }
}
