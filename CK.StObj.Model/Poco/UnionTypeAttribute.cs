using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Defines multiples allowed types on a Poco property.
    /// This attribute can be applied only on IPoco properties.
    /// The property type must be compatible with each of the <see cref="Types"/>.
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
