using System;

namespace CK.Core
{
    /// <summary>
    /// Poco importer factory.
    /// </summary>
    [IsMultiple]
    public interface IPocoImporterFactory : ISingletonAutoService
    {
        /// <summary>
        /// Gets the base protocol name that this factory handled.
        /// See <see cref="PocoExtendedProtocolName.BaseName"/>.
        /// <para>
        /// This name doesn't identify a factory: more than one factory can
        /// handle the same base protocol name.
        /// </para>
        /// </summary>
        string BaseProtocolName { get; }

        /// <summary>
        /// Attempts to create/resolves an importer from a <see cref="PocoExtendedProtocolName"/>.
        /// The protocol <see cref="PocoExtendedProtocolName.BaseName"/> must be the same as <see cref="BaseProtocolName"/>
        /// otherwise an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="protocol">The protocol name.</param>
        /// <returns>The importer or null if not found.</returns>
        IPocoImporter? TryCreate( IActivityMonitor monitor, PocoExtendedProtocolName protocol );
    }
}
