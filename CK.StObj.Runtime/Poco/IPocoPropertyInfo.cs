using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes properties of a <see cref="IPocoPropertyInfo"/>.
    /// <para>
    /// Properties that describe the type have been designed as a union type: they are all mutually exclusive
    /// and one of them is necessarily set (see <see cref="PocoPropertyKind"/>).
    /// </para>
    /// <para>
    /// This is defined by at least one <see cref="Implementations"/>. When more than one exists, they must
    /// be compatible across the different interfaces.
    /// </para>
    /// </summary>
    public interface IPocoPropertyInfo : IPocoBasePropertyInfo
    {
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
        /// Gets whether this property is read only.
        /// <list type="bullet">
        ///     <item>All the <see cref="IPocoPropertyInfo.Implementations"/> are read only.</item>
        ///     <item>The property is necessarily NOT nullable.</item>
        ///     <item>Property's type is necessarily:
        ///         <list type="bullet">
        ///             <item>Another family of <see cref="IPoco"/>.</item>
        ///             <item>Or a standard collection (HashSet&lt;&gt;, List&lt;&gt;, Dictionary&lt;,&gt;) but it cannot be an array.</item>
        ///         </list>
        ///     </item>
        /// </list>
        /// The property must be instantiated by the constructor of the Poco.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets the property name.
        /// </summary>
        string PropertyName { get; }

        /// <summary>
        /// Gets the property type.
        /// Possibly a nullable value type: use <see cref="PropertyNullableTypeTree"/> to get the (lifted) non-nullable type.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets the <see cref="NullableTypeTree"/> of this property.
        /// For union, this is the often an object (that can be nullable) but may be a IPoco.
        /// </summary>
        NullableTypeTree PropertyNullableTypeTree { get; }

        /// <summary>
        /// Gets the Poco property type of this property.
        /// </summary>
        PocoPropertyKind PocoPropertyKind { get; }

        /// <summary>
        /// Gets whether this property is instantiated (for IPoco and collections) or
        /// is set to its default value by the constructor.
        /// </summary>
        PocoConstructorAction ConstructorAction { get; }

        /// <summary>
        /// Gets the Poco root information if this <see cref="PropertyType"/> is a <see cref="IPoco"/>.
        /// </summary>
        IPocoRootInfo? PocoType { get; }

        /// <summary>
        /// Gets whether this property is nullable (simple relay to PropertyNullableTypeTree.Kind).
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Gets the default value if at least one <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined.
        /// If this is not null then <see cref="IPocoBasePropertyInfo.IsReadOnly"/> is necessarily false.
        /// <para>
        /// If the default value is defined by more than one interface, it must be the same.
        /// </para>
        /// <para>
        /// This default value must be set in the Poco constructor.
        /// </para>
        /// </summary>
        IPocoPropertyDefaultValue? DefaultValue { get; }

        /// <summary>
        /// Gets the set of types that defines the union type in their non-nullable form.
        /// Empty when not applicable. When applicable the actual type of the property is necessarily compatible
        /// (assignable to) any of the variants: this is usually object but can be a IPoco definer for instance.
        /// Nullability applies to the whole property.
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
        /// Gets all the property implementation across the different interfaces.
        /// </summary>
        IReadOnlyList<IPocoPropertyImpl> Implementations { get; }
    }
}
