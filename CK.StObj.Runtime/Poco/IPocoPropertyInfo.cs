using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes Poco property.
    /// </summary>
    public interface IPocoPropertyInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets whether this property is a <see cref="IPoco"/> or a ISet&lt;&gt;, Set&lt;&gt;, IList&lt;&gt;, List&lt;&gt;, IDictionary&lt;,&gt; or Dictionary&lt;,&gt;
        /// and that at least one of the <see cref="DeclaredProperties"/> is read only.
        /// </summary>
        bool AutoInstantiated { get; }

        /// <summary>
        /// Gets whether this property has no setter across all the interfaces where it appears.
        /// If at least one of the <see cref="DeclaredProperties"/> defines a setter then a setter will eventually be generated
        /// even if <see cref="AutoInstantiated"/> is true.
        /// </summary>
        bool HasDeclaredSetter { get; }

        /// <summary>
        /// Gets whether at least one <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined.
        /// Note that if the default value is defined by more than one interface, it is guaranteed to be the same.
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
        /// Gets the index of this property in the Poco class.
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
        /// </summary>
        NullabilityTypeInfo PropertyNullabilityInfo { get; }

        /// <summary>
        /// Gets the <see cref="NullableTypeTree"/> of this property.
        /// </summary>
        NullableTypeTree PropertyNullableTypeTree { get; }

        /// <summary>
        /// Gets whether this property may eventually be null: either it is a union with at least one <see cref="NullabilityTypeKind.IsNullable"/>
        /// flag, or it is not an union and the <see cref="PropertyNullabilityInfo"/> itself has the flag set.
        /// </summary>
        bool IsEventuallyNullable { get; }

        /// <summary>
        /// Gets the set of types that defines the union type with their nullability kind.
        /// Empty when not applicable. When applicable <see cref="PropertyType"/> is necessarily a type
        /// assignable to any of the variants.
        /// </summary>
        IEnumerable<(Type Type, NullabilityTypeKind Kind)> PropertyUnionTypes { get; }

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
