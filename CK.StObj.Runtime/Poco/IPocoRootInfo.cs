using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Defines information for a unified Poco type.
    /// </summary>
    public interface IPocoRootInfo
    {
        /// <summary>
        /// Gets the final, unified, type that implements all <see cref="Interfaces"/>.
        /// </summary>
        Type PocoClass { get; }

        /// <summary>
        /// Gets all the <see cref="IPocoInterfaceInfo"/> that this Poco implements.
        /// </summary>
        IReadOnlyList<IPocoInterfaceInfo> Interfaces { get; }

    }

}
