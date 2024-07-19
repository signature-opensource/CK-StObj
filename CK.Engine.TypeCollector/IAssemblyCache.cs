using System.Collections.Generic;
using System.Reflection;

namespace CK.Engine.TypeCollector
{
    public interface IAssemblyCache
    {
        /// <summary>
        /// Gets the cached assemblies. CachedAssembly are indexed by their Assembly and their simple name.
        /// </summary>
        IReadOnlyDictionary<object,CachedAssembly> Assemblies { get; }

        /// <summary>
        /// This can be called only once registrations are closed.
        /// </summary>
        /// <param name="assembly">The assembly to find or register. Must not be <see cref="Assembly.IsDynamic"/>.</param>
        /// <returns>The cached assembly.</returns>
        CachedAssembly FindOrCreate( Assembly assembly );
    }
}
