using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// An abstract field appears in <see cref="IAbstractPocoType.Fields"/>.
    /// The exchangeability is the type's <see cref="IPocoType.IsExchangeable"/>.
    /// </summary>
    public interface IAbstractPocoField
    {
        /// <summary>
        /// Gets the field name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the field main type.
        /// </summary>
        IPocoType Type { get; }

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
        /// Gets the property info that define this field.
        /// </summary>
        PropertyInfo Originator { get; }
    }
}
