using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Associates to <see cref="IPoco"/> interface its final, unified, implementation 
    /// and its <see cref="IPocoFactory{T}"/> interface type.
    /// </summary>
    public interface IPocoInterfaceInfo
    {
        /// <summary>
        /// Gets the IPoco interface.
        /// </summary>
        Type PocoInterface { get; }

        /// <summary>
        /// Gets the concrete, final, unified Poco type information.
        /// </summary>
        IPocoRootInfo Root { get; }

        /// <summary>
        /// Gets the <see cref="IPocoFactory{T}"/> where T is <see cref="PocoInterface"/> type.
        /// </summary>
        Type PocoFactoryInterface { get; }

    }
}
