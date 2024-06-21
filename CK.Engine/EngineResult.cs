using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Setup
{
    /// <summary>
    /// Captures the result of an engine run.
    /// </summary>
    public sealed class EngineResult
    {
        readonly bool _success;
        readonly ImmutableArray<IRunningBinPathGroup> _groups;

        internal EngineResult( StObjEngineResult r )
        {
            _success = r.Success;
            _groups = r.Groups.ToImmutableArray();
        }

        internal EngineResult( bool success, IRunningEngineConfiguration? c )
        {
            _success = success;
            _groups = c?.Groups.ToImmutableArray() ?? ImmutableArray<IRunningBinPathGroup>.Empty;
        }

        /// <summary>
        /// Gets whether the run succeeded.
        /// </summary>
        public bool Success => _success;

        /// <inheritdoc cref="IRunningEngineConfiguration.Groups"/>.
        public IReadOnlyList<IRunningBinPathGroup> Groups => _groups;
    }
}
