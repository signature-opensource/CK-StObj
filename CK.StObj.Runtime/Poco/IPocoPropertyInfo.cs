using CK.CodeGen;
using CK.Core;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes a IPoco property.
    /// This is defined by at least one <see cref="DeclaredProperties"/>. When more than one exists, they must
    /// be compatible across the different interfaces.
    /// </summary>
    public interface IPocoPropertyInfo : IPocoBasePropertyInfo
    {
        /// <summary>
        /// Gets the default value if at least one <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined.
        /// If this is true then <see cref="IPocoBasePropertyInfo.IsReadOnly"/> is necessarily false.
        /// <para>
        /// If the default value is defined by more than one interface, it is the same.
        /// </para>
        /// </summary>
        IPocoPropertyDefaultValue? DefaultValue { get; }

        /// <summary>
        /// Gets the set of types that defines the union type in their non-nullable form.
        /// Empty when not applicable. When applicable the actual type of the property is necessarily compatible
        /// (assignable to) any of the variants. Nullability applies to the whole property.
        /// <para>
        /// The set of possible types is "cleaned up":
        /// <list type="bullet">
        ///     <item>Only one instance of duplicated types is kept.</item>
        ///     <item>When a type and its specializations appear (IsAssginableFrom), only the most general one is kept.</item>
        /// </list>
        /// These rules guaranty that there is no duplicated actual <see cref="NullableTypeTree.Type"/> in any Union.
        /// </para>
        /// </summary>
        IEnumerable<NullableTypeTree> PropertyUnionTypes { get; }

        /// <summary>
        /// Gets the property declarations from the different <see cref="IPoco"/> interfaces (use <see cref="PropertyInfo.MemberType"/> to
        /// get the declaring interface).
        /// <para>
        /// During the discovery of all the IPoco interfaces of the family, this contains all the properties. The property type and read only analysis
        /// is done after the discovery phase and valid "abstract read only properties" are moved to the <see cref="AbstractReadOnlyProperties"/>.
        /// </para>
        /// <para>
        /// A valid IPoco eventually has an homogeneous set of properties in this list: they are either all readonly or not and their type
        /// is compatible (see <see cref="IPocoBasePropertyInfo.IsReadOnly"/>).
        /// </para>
        /// </summary>
        IReadOnlyList<PropertyInfo> DeclaredProperties { get; }

        /// <summary>
        /// Gets all the property implementation across the different interfaces.
        /// </summary>
        IReadOnlyList<IPocoPropertyImpl> Implementations { get; }
    }
}
