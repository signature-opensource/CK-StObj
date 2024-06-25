using System.Collections.Generic;

namespace CK.Engine.TypeCollector
{
    public sealed partial class BinPathTypeGroup
    {
        /// <summary>
        /// Result of a <see cref="Run"/>.
        /// </summary>
        public sealed class Result
        {
            readonly AssemblyCache.Result _assemblyResult;
            readonly IReadOnlyList<BinPathTypeGroup> _groups;

            internal Result( AssemblyCache.Result assemblyResult, IReadOnlyList<BinPathTypeGroup> groups )
            {
                _assemblyResult = assemblyResult;
                _groups = groups;
            }

            /// <summary>
            /// Gets whether all <see cref="BinPathTypeGroup"/> are on success.
            /// </summary>
            public bool Success => _assemblyResult.Success;

            /// <summary>
            /// Gets the ordered set of groups to setup.
            /// When there's more than one group, the first group's types is guaranteed to contain all the IRealObject and
            /// IPoco of all the groups and it may be a "pure unified group" (see <see cref="IsUnifiedPure"/>).
            /// </summary>
            public IReadOnlyList<BinPathTypeGroup> Groups => _groups;
        }
    }
}
