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
        /// The field is an allocated <see cref="IPrimaryPocoType"/> or <see cref="ISecondaryPocoType"/>
        /// or a <see cref="ICollectionPocoType.IsAbstractReadOnly"/> collection.
        /// </summary>
        ReadOnly,

        /// <summary>
        /// The field is a regular property with a setter.
        /// Its type can be an array but not another kind of collection.
        /// </summary>
        HasSetter,

        /// <summary>
        /// The field is a ref property. Its type is a <see cref="IRecordPocoType"/>.
        /// </summary>
        IsByRef,

        /// <summary>
        /// The field has no setter and is a <see cref="ICollectionPocoType"/> (a list, set
        /// or dictionary but not an array).
        /// </summary>
        MutableCollection
    }
}
