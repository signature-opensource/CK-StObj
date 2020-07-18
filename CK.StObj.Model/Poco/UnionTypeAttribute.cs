using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Defines multiples allowed types on an "object" property.
    /// This attribute can be applied only on "object" IPoco properties.
    /// Currently this always allows null value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UnionTypeAttribute : Attribute
    {
        /// <summary>
        /// Iniitalizes a new <see cref="UnionTypeAttribute"/>.
        /// </summary>
        /// <param name="types">The allowed types of this union.</param>
        public UnionTypeAttribute( params Type[] types )
        {
            Types = types;
        }

        /// <summary>
        /// Gets the allowed types.
        /// </summary>
        public IReadOnlyList<Type> Types { get; }
    }
}
