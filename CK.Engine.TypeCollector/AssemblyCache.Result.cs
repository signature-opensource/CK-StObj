using CK.Core;
using CK.Setup;
using System.Collections.Generic;
using System.Reflection;
using static CK.Core.CheckedWriteStream;

namespace CK.Engine.TypeCollector
{
    public sealed partial class AssemblyCache
    {

        /// <summary>
        /// Encapsulates the work of this <see cref="AssemblyCache"/> on an engine configuration.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to process.</param>
        /// <returns>The result or null on error.</returns>
        public static Result? ProcessEngineConfiguration( IActivityMonitor monitor, EngineConfiguration configuration )
        {
            bool success = true;
            var c = new AssemblyCache( configuration.ExcludedAssemblies.Contains );
            foreach( var b in configuration.BinPaths )
            {
                success &= c.Register( monitor, b ) != null;
            }
            var binPaths = c.CloseRegistrations( monitor );
            return binPaths != null ? new Result( c, binPaths ) : null;
        }

        /// <summary>
        /// Result of a successful <see cref="ProcessEngineConfiguration"/>.
        /// </summary>
        public sealed class Result : IAssemblyCache
        {
            readonly AssemblyCache _cache;
            readonly IReadOnlyCollection<BinPathGroup> _binPaths;

            internal Result( AssemblyCache cache, IReadOnlyCollection<BinPathGroup> binPaths )
            {
                _cache = cache;
                _binPaths = binPaths;
            }

            /// <inheritdoc />
            public IReadOnlyCollection<CachedAssembly> Assemblies => _cache.Assemblies;

            /// <inheritdoc />
            public CachedAssembly FindOrCreate( Assembly assembly ) => _cache.FindOrCreate( assembly );

            /// <summary>
            /// Gets the <see cref="BinPathGroup"/> with their similar assembly related <see cref="BinPathGroup.Configurations"/>.
            /// </summary>
            public IReadOnlyCollection<BinPathGroup> BinPathGroups => _binPaths;
        }
    }
}
