using CK.Core;
using System.Collections.Generic;

namespace CK.Setup;

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
    /// Configuration objects exposed here (including the root <see cref="IRunningBinPathGroup.EngineConfiguration"/>) must not be altered.
    /// </para>
    /// </summary>
    IRunningBinPathGroup ConfigurationGroup { get; }

    /// <summary>
    /// Gets a local service container, scoped to this path. This local container is backed by
    /// the <see cref="IStObjEngineRunContext.ServiceContainer"/> (as its <see cref="SimpleServiceContainer.BaseProvider"/>).
    /// <see cref="ICSCodeGenerator.Implement"/> and <see cref="IAutoImplementor{T}.Implement"/> typically registers services
    /// inside this container so that deferred implementors (see <see cref="CSCodeGenerationResult"/>) can depend on them.
    /// </summary>
    ISimpleServiceContainer ServiceContainer { get; }

    /// <summary>
    /// Gets a shared dictionary associated to this generated bin path. 
    /// Use of such shared memory should be avoided as much as possible, and if required should be properly encapsulated,
    /// typically by extension methods on this generated bin path.
    /// </summary>
    IDictionary<object, object?> Memory { get; }
}
