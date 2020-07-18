using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Defines multiples allowed types on an "object" property.
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
        }
    }
}
