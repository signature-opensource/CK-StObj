using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Summarized the vasic information about a <see cref="DIContainerDefinition"/>.
    /// </summary>
    public interface IDIContainerInfo
    {
        /// <summary>
        /// Gets the container name (this is the container definition type name without "DIContainerDefinition" suffix).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the Backend vs. Endpoint kind of this container.
        /// </summary>
        DIContainerKind Kind { get; }

        /// <summary>
        /// Gets the container definition.
        /// </summary>
        IStObjResult DIContainerDefinition { get; }

        /// <summary>
        ///  Gets the instance data type.
        /// </summary>
        Type? ScopeDataType { get; }
    }
}
