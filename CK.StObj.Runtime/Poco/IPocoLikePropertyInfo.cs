using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Describes a Poco-like property.
    /// </summary>
    public interface IPocoLikePropertyInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets whether this property is a readonly <see cref="IPoco"/>, a Poco-like object, ISet&lt;&gt;,
        /// Set&lt;&gt;, IList&lt;&gt;, List&lt;&gt;, IDictionary&lt;,&gt; or Dictionary&lt;,&gt;.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets the index of this property in the <see cref="IPocoLikeInfo.PropertyList"/>.
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
        /// Gets the property name.
        /// </summary>
        string PropertyName { get; }
    }
}
