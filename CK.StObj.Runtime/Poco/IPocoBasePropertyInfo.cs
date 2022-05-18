using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes shared properties of <see cref="IPocoClassPropertyInfo"/> and <see cref="IPocoPropertyInfo"/>.
    /// <para>
    /// Properties that describe the type have been designed as a union type: they are all mutually exclusive
    /// and one of them is necessarily set.
    /// </para>
    /// <para>
    /// For instance, when <see cref="IsBasicPropertyType"/> is true, then <see cref="IsStandardCollectionType"/>, <see cref="IsTupleType"/>,
    /// <see cref="IsEnumType"/> and <see cref="IsUnionType"/> are all false and <see cref="PocoClassType"/> and <see cref="PocoType"/> are both null.
    /// </para>
    /// <para>
    /// The <see cref="PocoClassType"/> acts as a fallback: a <see cref="IPocoClassInfo"/> is created for any type that is not a IPoco, a standard collection,
    /// an enumeration or a basic property. These Poco-like objects "close" the Poco's type space onto which serializers/exporters/marshalers rely to
    /// handle "allowed" types.
    /// </para>
    /// </summary>
    public interface IPocoBasePropertyInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets the index of this property in the <see cref="IPocoRootInfo.PropertyList"/> or <see cref="IPocoClassPropertyInfo.PropertyList"/>.
        /// Indexes starts at 0 and are compact: this can be used to handle optimized serialization
        /// by index (MessagePack) rather than by name (Json).
        /// <para>
        /// Note that for <see cref="IPocoPropertyInfo"/> the generated backing field is named <c>_v{Index}</c>.
        /// </para>
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets whether this property is read only.
        /// <para>
        /// For <see cref="IPocoPropertyInfo"/>:
        /// <list type="bullet">
        ///     <item>All the <see cref="DeclaredProperties"/> are also read only.</item>
        ///     <item>This property is necessarily NOT nullable.</item>
        ///     <item><see cref="IsUnionType"/> is necessarily false.</item>
        ///     <item>Property's type is necessarily:
        ///         <list type="bullet">
        ///             <item>Another family of <see cref="IPoco"/>...</item>
        ///             <item>...or a Poco-like object with a true <see cref="IPocoClassInfo.IsDefaultNewable"/>...</item>
        ///             <item>...or a standard collection (HashSet&lt;&gt;, List&lt;&gt;, Dictionary&lt;,&gt; but it cannot be an array.</item>
        ///         </list>
        ///     </item>
        /// </list>
        /// </para>
        /// <para>
        /// For <see cref="IPocoClassPropertyInfo"/>, there is no such restriction but to make serialization/export/marshalling easy
        /// (or even automatically doable), the property's type should be as simple as possible...
        /// </para>
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets the property name.
        /// </summary>
        string PropertyName { get; }

        /// <summary>
        /// Gets the property type.
        /// Possibly a nullable struct: use <see cref="PropertyNullableTypeTree"/> to get the (lifted) non-nullable type.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets the <see cref="NullableTypeTree"/> of this property.
        /// </summary>
        NullableTypeTree PropertyNullableTypeTree { get; }

        /// <summary>
        /// Gets whether at least one <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined.
        /// If this is true then <see cref="IsReadOnly"/> is necessarily false.
        /// <para>
        /// Applies to <see cref="IPocoPropertyInfo"/>: if the default value is defined by more than one interface,
        /// it must be the same (this is checked).
        /// </para>
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
        /// Gets the Poco-like information if this <see cref="PropertyType"/> is not one of the other
        /// property type.
        /// </summary>
        IPocoClassInfo? PocoClassType { get; }

        /// <summary>
        /// Gets the Poco root information if this <see cref="PropertyType"/> is a <see cref="IPoco"/>.
        /// </summary>
        IPocoRootInfo? PocoType { get; }

        /// <summary>
        /// Gets whether this property is a standard collection: an array, HashSet&lt;&gt;,
        /// List&lt;&gt;, or Dictionary&lt;,&gt;.
        /// </summary>
        bool IsStandardCollectionType { get; }

        /// <summary>
        /// Gets whether this property is a union type.
        /// This is always false for <see cref="IPocoClassPropertyInfo"/>.
        /// </summary>
        bool IsUnionType { get; }

        /// <summary>
        /// Gets whether this property is a <see cref="ValueTuple"/>.
        /// </summary>
        public bool IsTupleType => PropertyNullableTypeTree.Kind.IsTupleType();

        /// <summary>
        /// Gets whether this property is an <see cref="Enum"/>.
        /// </summary>
        public bool IsEnumType => PropertyNullableTypeTree.Kind.IsValueType() && PropertyNullableTypeTree.Type.IsEnum;

        /// <summary>
        /// Gets whether this property is a basic property (see <see cref="PocoSupportResultExtension.IsBasicPropertyType(Type)"/>).
        /// </summary>
        bool IsBasicPropertyType { get; }

        /// <summary>
        /// Gets whether this property is nullable (simple relay to PropertyNullableTypeTree.Kind).
        /// </summary>
        public bool IsNullable => PropertyNullableTypeTree.Kind.IsNullable();
    }
}
