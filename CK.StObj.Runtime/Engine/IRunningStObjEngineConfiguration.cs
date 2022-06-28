using CK.Core;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Extended <see cref="StObjEngineConfiguration"/> for the engine.
    /// </summary>
    public interface IRunningStObjEngineConfiguration
    {
        /// <inheritdoc cref="StObjEngineConfiguration{T}.Aspect"/>
        IReadOnlyList<IStObjEngineAspectConfiguration> Aspects { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.GeneratedAssemblyName"/>
        string GeneratedAssemblyName { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.BasePath"/>
        NormalizedPath BasePath { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.BaseSHA1"/>
        SHA1Value BaseSHA1 { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.ForceRun"/>
        bool ForceRun { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.BinPaths"/>
        IReadOnlyList<IRunningBinPathConfiguration> BinPaths { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.GlobalExcludedTypes"/>
        IReadOnlySet<string> GlobalExcludedTypes { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.InformationalVersion"/>
        string? InformationalVersion { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.RevertOrderingNames"/>
        bool RevertOrderingNames { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.TraceDependencySorterInput"/>
        bool TraceDependencySorterInput { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.TraceDependencySorterOutput"/>
        bool TraceDependencySorterOutput { get; }

        /// <inheritdoc cref="StObjEngineConfiguration{T}.ToXml"/>
        XElement ToXml();
    }
}
