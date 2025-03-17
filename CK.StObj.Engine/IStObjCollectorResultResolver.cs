namespace CK.Setup;

/// <summary>
/// Optional interface that enables to replace the configuration based <see cref="StObjCollector"/>
/// scaffolding with already available <see cref="StObjCollectorResult"/> thanks
/// to <see cref="StObjEngine.Run(IStObjCollectorResultResolver)"/>.
/// </summary>
public interface IStObjCollectorResultResolver
{
    /// <summary>
    /// Obtains the <see cref="StObjCollectorResult"/> for the similar group of bin paths.
    /// </summary>
    /// <param name="g">The group of bin paths for which a collector result must be provided.</param>
    /// <returns>The collector or null on error.</returns>
    StObjCollectorResult? GetResult( RunningBinPathGroup g );
}
