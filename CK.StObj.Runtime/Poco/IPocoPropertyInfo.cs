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
    public interface IPocoPropertyInfo
    {
        /// <summary>
        /// Gets whether this property is a <see cref="IPoco"/> or a ISet&lt;&gt;, Set&lt;&gt;, IList&lt;&gt;, List&lt;&gt;, IDictionary&lt;,&gt; or Dictionary&lt;,&gt;
        /// and that at least of the <see cref="DeclaredProperties"/> is read only.
        /// </summary>
        bool AutoInstantiated { get; }

        /// <summary>
        /// Gets the property type.
        /// </summary>
        Type PropertyType { get; }

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
