using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Context that is given to <see cref="IStObjEngineAspect.Configure"/> method.
    /// </summary>
    public interface IStObjEngineConfigureContext
    {
        /// <summary>
        /// Gets engine status information.
        /// </summary>
        IStObjEngineStatus EngineStatus { get; }

        /// <summary>
        /// Gets the external configuration object.
        /// </summary>
        StObjEngineConfiguration ExternalConfiguration { get; }

        /// <summary>
        /// Gets the service container into which services provided by aspects can be registered
        /// Concrete type mapping to aspects instances are automatically registered.
        /// </summary>
        ISimpleServiceContainer ServiceContainer { get; }

        /// <summary>
        /// Registers a configuration only service. This is used to comunicate the fact that the registered service
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
        /// Registers a type that must be a class or a IPoco interface.
        /// Aspects can use this instead of adding the assembly qualified name of the type in <see cref="BinPath.Types"/>.
        /// </summary>
        /// <param name="type">Type to register.</param>
        void AddExplicitRegisteredType( Type type );

        /// <summary>
        /// Gets the list of already created and configured aspects.
        /// Recall that the order of the configurations in <see cref="StObjEngineConfiguration.Aspects"/> drives the order of Aspects creation).
        /// When <see cref="IStObjEngineAspect.Configure"/> is called, only configured aspects are registered here and available.
        /// </summary>
        IReadOnlyList<IStObjEngineAspect> Aspects { get; }

        /// <summary>
        /// Pushes a defered configure action.
        /// It will be executed after the configuration of all aspects.
        /// An action can be pushed at any moment and a pushed action can push another action.
        /// </summary>
        /// <param name="postAction">Action to execute.</param>
        void PushPostConfigureAction( Func<IActivityMonitor, IStObjEngineConfigureContext, bool> postAction );
    }

}
