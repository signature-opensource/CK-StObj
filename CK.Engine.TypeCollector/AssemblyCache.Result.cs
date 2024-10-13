using CK.Core;
using CK.Setup;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

public sealed partial class AssemblyCache // Result
{
    /// <summary>
    /// Result of a <see cref="Run"/>: exposes the <see cref="AssemblyCache"/> and the <see cref="BinPathGroups"/>.
    /// </summary>
    public sealed class Result
    {
        readonly AssemblyCache _cache;
        readonly IReadOnlyCollection<BinPathGroup> _binPaths;
        readonly bool _success;

        internal Result( bool success, AssemblyCache cache, IReadOnlyCollection<BinPathGroup> binPaths )
        {
            _success = success;
            _cache = cache;
            _binPaths = binPaths;
        }

        /// <summary>
        /// Gets whether all <see cref="BinPathGroups"/> are on success.
        /// </summary>
        public bool Success => _success;

        /// <summary>
        /// Gets the assembly cache.
        /// </summary>
        public AssemblyCache AssemblyCache => _cache;

        /// <summary>
        /// Gets the <see cref="BinPathGroup"/> with their similar assembly related <see cref="BinPathGroup.Configurations"/>.
        /// </summary>
        public IReadOnlyCollection<BinPathGroup> BinPathGroups => _binPaths;

    }
}
