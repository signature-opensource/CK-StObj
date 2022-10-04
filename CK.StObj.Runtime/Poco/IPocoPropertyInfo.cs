using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes a IPoco property.
    /// This handles potentially more than one <see cref="DeclaredProperties"/> that must be identical across the different interfaces.
    /// </summary>
    public interface IPocoPropertyInfo : IPocoBasePropertyInfo
    {
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
        /// These rules guaranty that there is no duplicated actual <see cref="NullableTypeTree.Type"/> in any Union .
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
        /// Abstract readonly properties have a false <see cref="PropertyInfo.CanWrite"/> and a type that is compatible with (typically
        /// assignable from) the final type of this <see cref="IPocoBasePropertyInfo"/>.
        /// </summary>
        IReadOnlyList<PropertyInfo> AbstractReadOnlyProperties { get; }
    }
}
