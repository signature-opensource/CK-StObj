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
        /// Gets the field type.
        /// </summary>
        IPocoType Type { get; }

        /// <summary>
        /// Gets the property info that define this field.
        /// </summary>
        PropertyInfo Originator { get; }
    }
}
