using CK.Core;
using System.Collections.Generic;

namespace CK.Engine.TypeCollector;

public sealed partial class BinPathTypeGroup
{
    /// <summary>
    /// Result of a <see cref="Run"/>.
    /// </summary>
    public sealed class Result
    {
        readonly AssemblyCache.Result _assemblyResult;
        readonly IReadOnlyList<BinPathTypeGroup> _groups;
        readonly SHA1Value _signature;
        readonly bool _success;

        internal Result( AssemblyCache.Result assemblyResult, IReadOnlyList<BinPathTypeGroup> groups, SHA1Value signature, bool success )
        {
            _assemblyResult = assemblyResult;
            _groups = groups;
            _signature = signature;
            _success = success;
        }

        /// <summary>
        /// Gets whether all <see cref="BinPathTypeGroup"/> are on success.
        /// </summary>
        public bool Success => _success;

        /// <summary>
        /// Gets the assembly cache.
        /// </summary>
        public AssemblyCache AssemblyCache => _assemblyResult.AssemblyCache;

        /// <summary>
        /// Gets the global type cache.
        /// </summary>
        public GlobalTypeCache TypeCache => _assemblyResult.TypeCache;

        /// <summary>
        /// Gets the digital signature of this result based on each <see cref="Groups"/> signature.
        /// <para>
        /// <see cref="SHA1Value.Zero"/> if <see cref="Success"/> is false.
        /// </para>
        /// </summary>
        public SHA1Value Signature => _signature;

        /// <summary>
        /// Gets the ordered set of groups to setup.
        /// When there's more than one group, the first group's types is guaranteed to contain all the IRealObject and
        /// IPoco of all the groups and it may be a "pure unified group" (see <see cref="IsUnifiedPure"/>).
        /// </summary>
        public IReadOnlyList<BinPathTypeGroup> Groups => _groups;
    }
}
