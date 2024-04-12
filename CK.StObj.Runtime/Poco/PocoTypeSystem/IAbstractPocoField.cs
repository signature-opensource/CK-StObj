using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// An abstract field appears in <see cref="IAbstractPocoType.Fields"/>.
    /// <para>
    /// Only fields that have at least one <see cref="Implementations"/> exist.
    /// </para>
    /// </summary>
    public interface IAbstractPocoField : IBasePocoField
    {
        /// <summary>
        /// Gets whether the field is read only: it is not a <c>{ get; set; }</c> nor
        /// a <c>ref { get; }</c> property.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets the <see cref="IPrimaryPocoField"/> that implement this field.
        /// </summary>
        IEnumerable<IPrimaryPocoField> Implementations { get; }

        /// <summary>
        /// Gets the property info that defines this field.
        /// </summary>
        PropertyInfo Originator { get; }
    }
}
