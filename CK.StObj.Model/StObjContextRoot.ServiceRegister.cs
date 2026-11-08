using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

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
        /// <para>
        /// When no <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> is registered and the <see cref="Services"/>
        /// come from a host builder, <see cref="AddStObjMap(IStObjMap)"/> registers the builder's configuration.
        /// </para>
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
                    EnsureHostConfiguration();
                    if( !map.ConfigureServices( this ) ) result = false;
                }
                catch( Exception ex )
                {
                    Monitor.Error( "While registering StObjMap.", ex );
                }
            }
            return result;
        }

        // Temporary workaround, waiting for the Unified Configuration: the ConfigureServices methods can use the
        // IConfiguration of the host builder. When no IConfiguration is in the StartupServices, the one of the
        // HostBuilderContext (registered as an instance by the HostApplicationBuilder, unlike the IConfiguration
        // that is a factory) is used: it is the builder's Configuration.
        void EnsureHostConfiguration()
        {
            if( StartupServices.GetService( typeof( IConfiguration ) ) == null
                && Services.LastOrDefault( d => d.ServiceType == typeof( HostBuilderContext ) )?.ImplementationInstance is HostBuilderContext ctx
                && ctx.Configuration != null )
            {
                StartupServices.Add( typeof( IConfiguration ), ctx.Configuration );
                Monitor.Trace( "Using the IConfiguration of the HostBuilderContext as a startup service." );
            }
        }
    }
}
