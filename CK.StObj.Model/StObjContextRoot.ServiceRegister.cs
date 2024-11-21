using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Core;

public abstract partial class StObjContextRoot
{
    /// <summary>
    /// Small helper that captures the minimal required context to configure a <see cref="IServiceCollection"/>.
    /// The main method is <see cref="StObjServiceCollectionExtensions.AddStObjMap(IServiceCollection, IActivityMonitor, IStObjMap, SimpleServiceContainer)"/>.
    /// </summary>
    public readonly struct ServiceRegister
    {
        /// <summary>
        /// Initializes a new <see cref="ServiceRegister"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use. Must not be null.</param>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="startupServices">
        /// Optional simple container that may provide startup services. This is not used to build IRealObject
        /// (they must be independent of any "dynamic" services), however registered services become available to
        /// any <see cref="StObjContextRoot.ConfigureServicesMethodName"/> methods by parameter injection.
        /// </param>
        public ServiceRegister( IActivityMonitor monitor, IServiceCollection services, SimpleServiceContainer? startupServices = null )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( services );
            Monitor = monitor;
            Services = services;
            StartupServices = startupServices ?? new SimpleServiceContainer();
        }

        /// <summary>
        /// Gets the monitor to use.
        /// </summary>
        public IActivityMonitor Monitor { get; }

        /// <summary>
        /// Gets the target service collection.
        /// </summary>
        public IServiceCollection Services { get; }

        /// <summary>
        /// Gets the startup services container.
        /// These services is not used to build IRealObject (they must be independent of any "dynamic" services). These registered services
        /// become available to any <see cref="ConfigureServicesMethodName"/> methods through parameter injection.
        /// </summary>
        public SimpleServiceContainer StartupServices { get; }

        /// <summary>
        /// Registers the map, the Real objects, singleton services, scoped services and initialize
        /// any <see cref="IDIContainer{TScopeData}"/>.
        /// Caution: this never throws, instead any exception is logged and false is returned.
        /// </summary>
        /// <param name="map">The map to register. Must not be null.</param>
        /// <returns>
        /// True on success, false if any <see cref="LogLevel.Fatal"/> or <see cref="LogLevel.Error"/> has been logged or if an exception has been thrown.
        /// </returns>
        public bool AddStObjMap( IStObjMap map )
        {
            bool result = true;
            using( Monitor.OnError( () => result = false ) )
            using( Monitor.OpenInfo( "Configuring Service collection from StObjMap." ) )
            {
                try
                {
                    Throw.CheckNotNullArgument( map );
                    if( !map.ConfigureServices( this ) ) result = false;
                }
                catch( Exception ex )
                {
                    Monitor.Error( "While registering StObjMap.", ex );
                }
            }
            return result;
        }
    }
}
