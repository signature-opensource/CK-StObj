using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{

    /// <summary>
    /// Describes properties of <see cref="IPocoClassPropertyInfo"/> and <see cref="IPocoPropertyInfo"/>.
    /// <para>
    /// Properties that describe the type have been designed as a union type: they are all mutually exclusive
    /// and one of them is necessarily set: see <see cref="PocoPropertyKind"/>.
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
        /// Gets the index of this property in the <see cref="IPocoRootInfo.PropertyList"/> or <see cref="IPocoClassInfo.PropertyList"/>.
        /// Indexes starts at 0 and are compact: this can be used to handle optimized serialization
        /// by index (MessagePack) rather than by name (Json).
        /// <para>
        /// Note that for <see cref="IPocoPropertyInfo"/> the generated backing field is named <c>_v{Index}</c>.
        /// </para>
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets whether this property is read only.
        /// This applies only to IPoco (<see cref="IPocoPropertyInfo"/>), readonly properties are ignored on
        /// PocoClass (<see cref="IPocoClassInfo"/>) this is always false.
        /// <para>
        /// For <see cref="IPocoPropertyInfo"/>:
        /// <list type="bullet">
        ///     <item>All the <see cref="IPocoPropertyInfo.Implementations"/> are read only.</item>
        ///     <item>The property is necessarily NOT nullable.</item>
        ///     <item>Property's type is necessarily:
        ///         <list type="bullet">
        ///             <item>Another family of <see cref="IPoco"/>...</item>
        ///             <item>...or a Poco class.</item>
        ///             <item>...or a standard collection (HashSet&lt;&gt;, List&lt;&gt;, Dictionary&lt;,&gt;) but it cannot be an array.</item>
        ///         </list>
        ///     </item>
        ///     <item>The property must be instantiated by the constructor of the Poco.</item>
        /// </list>
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
        /// Gets the Poco property type of this property.
        /// </summary>
        PocoPropertyKind PocoPropertyKind { get; }

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
        /// Gets whether this property is nullable (simple relay to PropertyNullableTypeTree.Kind).
        /// </summary>
        public bool IsNullable => PropertyNullableTypeTree.Kind.IsNullable();
    }
}
