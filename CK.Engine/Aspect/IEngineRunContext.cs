
using CK.Core;
using System.Collections.Generic;
using System;

namespace CK.Setup;

/// <summary>
/// Context that is given to <see cref="EngineAspect.RunPreCode"/>, <see cref="EngineAspect.RunPostCode"/>
/// and <see cref="EngineAspect.Terminate"/> methods.
/// </summary>
public interface IEngineRunContext
{
    /// <summary>
    /// Gets the service container.
    /// Exposing the container itself during the run (instead of the mere <see cref="IServiceProvider"/>)
    /// enables aspects to dynamically register services while running.
    /// </summary>
    ISimpleServiceContainer ServiceContainer { get; }

    /// <summary>
    /// Gets the list of all available aspects.
    /// </summary>
    IReadOnlyList<EngineAspect> Aspects { get; }

    /// <summary>
    /// Gets the primary bin path.
    /// <para>
    /// This primary bin path is guaranteed to contain all the IRealObject and all the IPoco that are registered in all the other ones.
    /// This is the first to be processed and may be the "pure" working folder one (<see cref="IRunningBinPathGroup.IsUnifiedPure"/>).
    /// </para>
    /// </summary>
    IGeneratedBinPath PrimaryBinPath { get; }

    /// <summary>
    /// Gets all the <see cref="IGeneratedBinPath"/> including the <see cref="PrimaryBinPath"/> (that is the first one).
    /// </summary>
    IReadOnlyList<IGeneratedBinPath> AllBinPaths { get; }

    /// <summary>
    /// Pushes a deferred action.
    /// It will be executed after this RunPreCode, RunPostCode or Terminate call on all aspects.
    /// An action can be pushed at any moment and a pushed action can push another action.
    /// </summary>
    /// <param name="postAction">Action to execute.</param>
    void PushDeferredAction( Func<IActivityMonitor, bool> postAction );
}
