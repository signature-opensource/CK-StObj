using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Exposes a type to type mapping.
    /// </summary>
    public interface IStObjTypeMap
    {
        /// <summary>
        /// Gets the mapped type or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type.</param>
        /// <returns>Mapped type or null if no mapping exists for this type.</returns>
        Type ToLeafType( Type t );

        /// <summary>
        /// Gets whether a type is mapped.
        /// </summary>
        /// <param name="t">Type to lookup.</param>
        /// <returns>True if <paramref name="t"/> is mapped in this context, false otherwise.</returns>
        bool IsMapped( Type t );

        /// <summary>
        /// Gets all types mapped by this map.
        /// </summary>
        IEnumerable<Type> Types { get; }
    }
}
