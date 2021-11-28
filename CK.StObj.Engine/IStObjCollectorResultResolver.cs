using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Optional interface that enables to hook the <see cref="StObjCollector"/> work
    /// thanks to <see cref="StObjEngine.Run(IStObjCollectorResultResolver)"/>.
    /// </summary>
    public interface IStObjCollectorResultResolver
    {
        /// <summary>
        /// Computes the <see cref="StObjCollectorResult"/> for the unified bin path.
        /// </summary>
        /// <param name="unified">The unified bin path.</param>
        /// <returns>The collector or null on error.</returns>
        StObjCollectorResult? GetUnifiedResult( BinPathConfiguration unified );

        /// <summary>
        /// Computes a <see cref="StObjCollectorResult"/> for a set of equivalent bin paths.
        /// </summary>
        /// <param name="head">The configuration that represents the set.</param>
        /// <param name="all">The set of equivalent bin paths (including the <paramref name="head"/>.</param>
        /// <returns>The collector or null on error.</returns>
        StObjCollectorResult? GetSecondaryResult( BinPathConfiguration head, IEnumerable<BinPathConfiguration> all );
    }

}
