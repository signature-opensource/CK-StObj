
using CK.Core;
using System.Collections.Generic;
using System;

namespace CK.Setup;

/// <summary>
/// Context that is given to <see cref="EngineAspect.Initialize(IActivityMonitor, IEngineAspectInitializationContext)"/> method.
/// </summary>
public interface IEngineAspectInitializationContext
{
    /// <summary>
    /// Gets the service container into which services provided by aspects can be registered.
    /// </summary>
    ISimpleServiceContainer ServiceContainer { get; }

    /// <summary>
    /// Registers a configuration only service.
    /// The registered service can only be injected in the other following aspects constructor.
    /// </summary>
    /// <typeparam name="T">Type of the service.</typeparam>
    /// <param name="service">Service instance.</param>
    void AddConfigureOnlyService<T>( T service ) where T : class;

    /// <summary>
    /// Gets the root of the <see cref="StObjConfigurationLayer"/> chain of responsibility.
    /// Aspects can add any needed configuration layer to it.
    /// </summary>
    StObjEngineConfigurator Configurator { get; }

    /// <summary>
    /// Gets the list of already created and initialized aspects.
    /// Recall that the order of the configurations in <see cref="EngineConfiguration.Aspects"/> drives the order of Aspects creation).
    /// When <see cref="EngineAspect.Initialize"/> is called, only configured previous aspects are available.
    /// </summary>
    IReadOnlyList<EngineAspect> Aspects { get; }

    /// <summary>
    /// Pushes a deferred initialization action.
    /// It will be executed after the initialization of all aspects.
    /// An action can be pushed at any moment and a pushed action can push another action.
    /// </summary>
    /// <param name="postAction">Action to execute.</param>
    void PushDeferredAction( Func<IActivityMonitor, bool> postAction );
}
