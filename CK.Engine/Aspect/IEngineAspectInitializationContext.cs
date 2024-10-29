
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
    /// Gets the service container into which services provided by aspects can be registered
    /// Concrete type mapping to aspects instances are automatically registered.
    /// </summary>
    ISimpleServiceContainer ServiceContainer { get; }

    /// <summary>
    /// Registers a configuration only service. This is used to communicate the fact that the registered service
    /// should only be used by the other following aspects only from their <see cref="IStObjEngineAspect.Configure"/> method.
    /// </summary>
    /// <typeparam name="T">Actual type of the service.</typeparam>
    /// <param name="service">Strongly typed wrapper around a necessary not null service instance.</param>
    void AddConfigureOnlyService<T>( ConfigureOnly<T> service );

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
    /// Pushes a deferred configure action.
    /// It will be executed after the configuration of all aspects.
    /// An action can be pushed at any moment and a pushed action can push another action.
    /// </summary>
    /// <param name="postAction">Action to execute.</param>
    void PushPostConfigureAction( Func<IActivityMonitor, IStObjEngineConfigureContext, bool> postAction );
}
