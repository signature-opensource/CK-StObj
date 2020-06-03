using CK.Core;
using System;
using System.Collections.Generic;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Exposes the result of <see cref="IPoco"/> interfaces support.
    /// </summary>
    public interface IPocoSupportResult
    {
        /// <summary>
        /// The final factory type.
        /// </summary>
        Type FinalFactory { get; }

        /// <summary>
        /// Gets the root Poco information.
        /// </summary>
        IReadOnlyList<IPocoRootInfo> Roots { get; }

        /// <summary>
        /// Gets the <see cref="IPocoInterfaceInfo"/> for any <see cref="IPoco"/> interface.
        /// </summary>
        /// <param name="pocoInterface">The IPoco interface.</param>
        /// <returns>Information about the interface. Null if not found.</returns>
        IPocoInterfaceInfo? Find( Type pocoInterface );

        /// <summary>
        /// Gets the dictionary of all Poco interfaces indexed by their <see cref="IPocoInterfaceInfo.PocoInterface"/>.
        /// </summary>
        IReadOnlyDictionary<Type, IPocoInterfaceInfo> AllInterfaces { get; }

    }
}
