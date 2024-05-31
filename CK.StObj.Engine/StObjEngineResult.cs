using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Captures the result of a <see cref="StObjEngine"/>'s run.
    /// </summary>
    public sealed class StObjEngineResult
    {
        /// <summary>
        /// Gets whether the run succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets the engine configuration.
        /// This is the original configuration object that has been altered
        /// by the engine.
        /// </summary>
        public EngineConfiguration Configuration { get; }

        /// <inheritdoc cref="IRunningEngineConfiguration.Groups"/>.
        public IReadOnlyList<IRunningBinPathGroup> Groups { get; }

        internal StObjEngineResult( bool success, IRunningEngineConfiguration c )
        {
            Success = success;
            Configuration = c.Configuration;
            Groups = c.Groups;
        }

    }
}
