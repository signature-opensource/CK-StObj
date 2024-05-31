using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Associates the <see cref="Groups"/> of similar configurations and the mutable
    /// original engine configuration. Note that the original configuration is updated by the engine.
    /// </summary>
    public interface IRunningEngineConfiguration
    {
        /// <summary>
        /// Gets the engine configuration.
        /// Even if this configuration is technically mutable, it should not be altered.
        /// </summary>
        EngineConfiguration Configuration { get; }

        /// <summary>
        /// Gets the groups of similar <see cref="BinPathConfiguration"/>.
        /// <list type="bullet">
        ///     <item>
        ///     If it has been required, the purely unified BinPath is the single configuration
        ///     of the first group of this list.
        ///     </item>
        ///     <item>
        ///     If no unified BinPath is required, the first group of this list is guaranteed to "cover"
        ///     all the other groups.
        ///     </item>
        /// </list>
        /// <para>
        /// In both case, the first group is processed first: this is the <see cref="IStObjEngineRunContext.PrimaryBinPath"/>.
        /// </para>
        /// </summary>
        IReadOnlyList<IRunningBinPathGroup> Groups { get; }
    }
}
