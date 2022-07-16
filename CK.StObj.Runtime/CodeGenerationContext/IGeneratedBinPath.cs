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
        /// Gets the groups of similar <see cref="BinPathConfiguration"/>.
        /// <para>
        /// Configuration objects exposed here must not be altered.
        /// </para>
        /// </summary>
        IRunningBinPathGroup ConfigurationGroup { get; }

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
    }
}
