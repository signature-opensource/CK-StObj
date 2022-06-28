using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Captures the result of a BinPath analysis: multiple equivalent (compatible) <see cref="BinPathConfiguration"/>
    /// are bound to the same <see cref="EngineMap"/>.
    /// </summary>
    public interface IGeneratedBinPath
    {
        /// <summary>
        /// Gets the engine map that concentrates all the Real Objects, Poco and AutoServices information.
        /// </summary>
        IStObjEngineMap EngineMap { get; }

        /// <summary>
        /// Gets whether this <see cref="IGeneratedBinPath"/> is the purely unified one.
        /// <para>
        /// This unified BinPath has not the same Assemblies, ExcludedTypes and Types configurations as any of the actual BinPaths.
        /// This BinPath is only used to create an incomplete primary StObjMap (without AutoService resolution) that will
        /// contain all the IPoco and IRealObject. Generating the code for this BinPath will impact the real world with
        /// the unified types from all the BinPaths but this code will never be used.
        /// </para>
        /// </summary>
        bool IsUnifiedPure { get; }

        /// <summary>
        /// Gets one or more <see cref="IRunningBinPathConfiguration"/> that share/are compatible with this <see cref="EngineMap"/>.
        /// When used by tests Mock objects may not have any configuration.
        /// </summary>
        IReadOnlyCollection<IRunningBinPathConfiguration> BinPathConfigurations { get; }

        /// <summary>
        /// Gets a local service container, scoped to this path. This local container is backed by
        /// the <see cref="ICodeGenerationContext.GlobalServiceContainer"/> (see <see cref="SimpleServiceContainer.BaseProvider"/>).
        /// <see cref="ICSCodeGenerator.Implement"/> and <see cref="IAutoImplementor{T}.Implement"/> typically registers services
        /// inside this container so that deferred implementors (<see cref="CSCodeGenerationResult.ImplementorType"/>) can depend on them.
        /// <para>
        /// It contains the <see cref="IPocoSupportResult"/> for this path.
        /// </para>
        /// </summary>
        ISimpleServiceContainer ServiceContainer { get; }

        /// <summary>
        /// Gets a shared dictionary associated to this generated bin path. 
        /// Note that, just like the <see cref="ICodeGenerationContext.GlobalMemory"/>, use of such shared memory should be avoided as much as possible,
        /// and if required should be properly encapsulated, typically by extension methods on this generated bin path.
        /// </summary>
        IDictionary<object, object?> Memory { get; }

        /// <summary>
        /// Gets the name (or comma separated names) of the <see cref="BinPathConfigurations"/>.
        /// When no configuration exists, this is "CurrentTest" since only with Mock test objects can we have no BinPathConfigurations.
        /// </summary>
        public string Names => BinPathConfigurations.Count > 0
                                ? BinPathConfigurations.Select( c => String.IsNullOrEmpty( c.Name ) ? "(Unified)" : c.Name ).Concatenate()
                                : "CurrentTest";
    }
}
