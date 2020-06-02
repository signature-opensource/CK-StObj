using System;
using CK.Core;
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
        /// Gets whether the <see cref="IClosedPoco"/> interface marker appear among the interfaces.
        /// When this is true, then <see cref="ClosureInterface"/> is necessarily not null.
        /// </summary>
        bool IsClosedPoco { get; }

        /// <summary>
        /// Gets the IPoco interface that "closes" all these <see cref="Interfaces"/>: this interface "unifies"
        /// all the other ones.
        /// If <see cref="IsClosedPoco"/> is true, then this is necessarily not null.
        /// </summary>
        Type? ClosureInterface { get; }

        /// <summary>
        /// Gets all the <see cref="IPocoInterfaceInfo"/> that this Poco implements.
        /// </summary>
        IReadOnlyList<IPocoInterfaceInfo> Interfaces { get; }

    }

}
