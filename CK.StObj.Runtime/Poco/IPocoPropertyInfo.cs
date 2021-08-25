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
    public interface IPocoPropertyInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets whether this property is a <see cref="IPoco"/>, Poco-like objects, ISet&lt;&gt;, IList&lt;&gt; or IDictionary&lt;,&gt;
        /// AND that all the <see cref="DeclaredProperties"/> are read only AND that this property is NOT nullable AND <see cref="PropertyUnionTypes"/> is empty.
        /// <para>
        /// Note that DeclaredProperties must all be read/write (with a getter and a setter) or all be read only otherwise an error is raised.
        /// </para>
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets whether at least one <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined.
        /// Note that if the default value is defined by more than one interface, it must be the same (this is checked) and that if this
        /// is true then <see cref="IsReadOnly"/> is necessarily false (allowed readonly types are <see cref="IPoco"/>, Poco-like objects,
        /// ISet&lt;&gt;, Set&lt;&gt;, IList&lt;&gt;, List&lt;&gt;, IDictionary&lt;,&gt; or Dictionary&lt;,&gt; and cannot have default values).
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
        /// Gets the index of this property in the <see cref="IPocoRootInfo.PropertyList"/>.
        /// Indexes starts at 0 and are compact: this can be used to handle optimized serialization
        /// by index (MessagePack) rather than by name (Json).
        /// <para>
        /// Note that the generated backing field is named <c>_v{Index}</c>.
        /// </para>
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the property type.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets the <see cref="NullabilityTypeInfo"/> of this property.
        /// This drives the nullability of the <see cref="PropertyUnionTypes"/> (if any) that are not nullable.
        /// </summary>
        NullabilityTypeInfo PropertyNullabilityInfo { get; }

        /// <summary>
        /// Gets whether this property is nullable (simple relay to PropertyNullabilityInfo.Kind).
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Gets the <see cref="NullableTypeTree"/> of this property.
        /// </summary>
        NullableTypeTree PropertyNullableTypeTree { get; }

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
        /// Gets the property name.
        /// </summary>
        string PropertyName { get; }

        /// <summary>
        /// Gets the property declarations from the different <see cref="IPoco"/> interfaces (use <see cref="PropertyInfo.MemberType"/> to
        /// get the declaring interface).
        /// </summary>
        IReadOnlyList<PropertyInfo> DeclaredProperties { get; }
    }
}
