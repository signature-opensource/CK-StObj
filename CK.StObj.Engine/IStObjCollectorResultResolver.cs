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
        /// Computes the <see cref="StObjCollectorResult"/> for the bin path.
        /// </summary>
        /// <param name="binPath">The bin path for which a collector result must be provided.</param>
        /// <returns>The collector or null on error.</returns>
        StObjCollectorResult? GetResult( IRunningBinPathConfiguration binPath );
    }

}
