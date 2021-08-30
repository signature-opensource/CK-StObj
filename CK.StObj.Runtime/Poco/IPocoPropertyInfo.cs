using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes a Poco property.
    /// This handles potentially more than one <see cref="DeclaredProperties"/> that must be identical across the different interfaces.
    /// </summary>
    public interface IPocoPropertyInfo : IPocoBasePropertyInfo
    {
        /// <summary>
        /// Gets whether at least one <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined.
        /// Note that if the default value is defined by more than one interface, it must be the same (this is checked) and that if this
        /// is true then <see cref="IsReadOnly"/> is necessarily false.
        /// </summary>
        bool HasDefaultValue { get; }

        /// <summary>
        /// Gets the default value. This must be considered if and only if <see cref="HasDefaultValue"/> is true.
        /// If this property has no <see cref="System.ComponentModel.DefaultValueAttribute"/> (HasDefaultValue is false), this is null.
        /// </summary>
        object? DefaultValue { get; }

        /// <summary>
        /// Gets the default value as a source string or a null if this property has no <see cref="System.ComponentModel.DefaultValueAttribute"/>.
        /// </summary>
        string? DefaultValueSource { get; }

        /// <summary>
        /// Gets the set of types that defines the union type in their non-nullable form.
        /// Empty when not applicable. When applicable <see cref="PropertyType"/> is necessarily a type
        /// assignable to any of the variants. Nullability applies to the whole property: the <see cref="PropertyNullableTypeTree"/> defines it.
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
        /// </summary>
        IReadOnlyList<PropertyInfo> DeclaredProperties { get; }
    }
}
