
// This code is embedded in the generated code but in the CK.StObj namespace
// in its own namespace block so that the scoped "using" are only for this.
//
// It is the core of the endpoint container implementation.
// If anything here is changed, it has to be manually reported in the code generation
// (and vice versa).
namespace CK.StObj.Engine.Tests
{
    using CK.Core;
    using Microsoft.Extensions.DependencyInjection;
    using System.Collections.Generic;
    using System;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using System.Linq;

    interface IEndpointTypeInternal : IEndpointType
    {
        bool ConfigureServices( IActivityMonitor monitor, IStObjMap stObjMap, IServiceCollection commonEndpointContainer );
    }

    sealed class EndpointType<TScopeData> : IEndpointType<TScopeData>, IEndpointTypeInternal where TScopeData : notnull
    {
        internal EndpointServiceProvider<TScopeData>? _services;

        readonly EndpointDefinition<TScopeData> _definition;
        internal ServiceCollection? _configuration;
        readonly object _lock;
        bool _initializationSuccess;

        public EndpointType( EndpointDefinition<TScopeData> definition )
        {
            _definition = definition;
            _lock = new object();
        }

        public EndpointDefinition EndpointDefinition => _definition;

        public Type ScopeDataType => typeof( TScopeData );

        public string Name => _definition.Name;

        public EndpointServiceProvider<TScopeData> GetContainer() => _services ?? DoCreateContainer();

        EndpointServiceProvider<TScopeData> DoCreateContainer()
        {
            lock( _lock )
            {
                if( _services == null )
                {
                    if( !_initializationSuccess ) Throw.InvalidOperationException( "Endpoint initialization failed. It cannot be used." );
                    _services = new EndpointServiceProvider<TScopeData>( _configuration.BuildServiceProvider() );
                    // Release the configuration now that the endpoint container is built.
                    _configuration = null;
                }
                return _services;
            }
        }

        public bool ConfigureServices( IActivityMonitor monitor, IStObjMap stObjMap, IServiceCollection commonEndpointContainer )
        {
            var configuration = new ServiceCollection();
            // Calls the ConfigureEndpointServices on the empty configuration.
            _definition.ConfigureEndpointServices( configuration );
            // Process the registrations to detect:
            // - extra registrations: they must not be types mapped to IRealObject or IAutoService.
            // - missing registrations from the definition.
            if( CheckRegistrations( monitor, configuration, _definition, stObjMap ) )
            {
                configuration.AddRange( commonEndpointContainer );
                // Add the scoped data holder.
                var scopedDataType = typeof( EndpointScopeData<TScopeData> );
                configuration.Add( new ServiceDescriptor( scopedDataType, scopedDataType, ServiceLifetime.Scoped ) );
                // Waiting for .Net 8.
                // configuration.MakeReadOnly();
                _configuration = configuration;
                return _initializationSuccess = true;
            }
            return false;

            static bool CheckRegistrations( IActivityMonitor monitor, ServiceCollection configuration, EndpointDefinition definition, IStObjMap stObjMap )
            {
                var handledSingletons = new List<Type>( definition.SingletonServices );
                var handledScoped = new List<Type>( definition.ScopedServices );
                List<Type>? moreSingletons = null;
                List<Type>? moreScoped = null;
                foreach( var d in configuration )
                {
                    if( d.Lifetime == ServiceLifetime.Singleton )
                    {
                        if( !handledSingletons.Remove( d.ServiceType ) )
                        {
                            moreSingletons ??= new List<Type>();
                            moreSingletons.Add( d.ServiceType );
                        }
                    }
                    else
                    {
                        if( !handledScoped.Remove( d.ServiceType ) )
                        {
                            moreScoped ??= new List<Type>();
                            moreScoped.Add( d.ServiceType );
                        }
                    }
                }
                bool success = ErrorUnhandledServices( monitor, definition, handledSingletons, ServiceLifetime.Singleton );
                if( !ErrorUnhandledServices( monitor, definition, handledScoped, ServiceLifetime.Scoped ) ) success = false;
                if( !ErrorNotEndpointAutoServices( monitor, definition, stObjMap, moreSingletons, ServiceLifetime.Singleton ) ) success = false;
                if( !ErrorNotEndpointAutoServices( monitor, definition, stObjMap, moreScoped, ServiceLifetime.Scoped ) ) success = false;
                return success;

                static bool ErrorNotEndpointAutoServices( IActivityMonitor monitor, EndpointDefinition definition, IStObjMap stObjMap, List<Type>? extra, ServiceLifetime lt )
                {
                    bool success = true;
                    if( extra != null )
                    {
                        foreach( var s in extra )
                        {
                            var autoMap = stObjMap.ToLeaf( s );
                            if( autoMap != null )
                            {
                                monitor.Error( $"Endpoint '{definition.Name}' configures the {lt} services: '{s:C}' that is automatically mapped to '{autoMap.ClassType:C}'. This is not allowed." );
                                success = false;
                            }
                            else
                            {
                                monitor.Warn( $"Endpoint '{definition.Name}' supports the {lt} service '{s:C}' that is not declared as a endpoint service." );
                            }
                        }
                    }
                    return success;
                }

                static bool ErrorUnhandledServices( IActivityMonitor monitor, EndpointDefinition definition, List<Type> unhandled, ServiceLifetime lt )
                {
                    if( unhandled.Count > 0 )
                    {
                        monitor.Error( $"Endpoint '{definition.Name}' doesn't handles the declared {lt} services: '{unhandled.Select( s => s.ToCSharpName() ).Concatenate( "', '" )}'." );
                        return false;
                    }
                    return true;
                }
            }
        }
    }

    static class EndpointHelper
    {
        internal static IServiceCollection CreateCommonEndpointContainer( IActivityMonitor monitor,
                                                                            IServiceCollection global,
                                                                            Func<Type, bool> isEndpointService,
                                                                            Dictionary<Type, object> externalMappings )
        {
            ServiceCollection endpoint = new ServiceCollection();
            foreach( var d in global )
            {
                var t = d.ServiceType;
                if( t == typeof( EndpointTypeManager ) ) Throw.ArgumentException( "EndpointTypeManager must not be configured." );
                // Skip any endpoint service and IHostedService.
                if( isEndpointService( t ) || t == typeof( Microsoft.Extensions.Hosting.IHostedService ) )
                {
                    // There's no need to have the IHostedService multiple service in the endpoint containers.
                    continue;
                }
                if( d.Lifetime == ServiceLifetime.Singleton )
                {
                    // If it's a singleton, we must add the relay to the Global only once.
                    if( !TrackMultiple( externalMappings, t, d ) )
                    {
                        // Configure the relay to the last registered singleton.
                        endpoint.AddSingleton( t, sp => sp.GetRequiredService<EndpointTypeManager>().GlobalServiceProvider.GetService( t )! );
                    }
                }
                else
                {
                    // For scope, this is simple: we reuse the service descriptor instance.
                    endpoint.Add( d );
                    // And we track duplicates to handle its IEnumerable<T> registration.
                    TrackMultiple( externalMappings, t, d );
                }
            }
            return endpoint;

            static bool TrackMultiple( Dictionary<Type, object> externalMultiple, Type t, ServiceDescriptor d )
            {
                if( externalMultiple.TryGetValue( t, out var exists ) )
                {
                    // If multiple registrations exist, memorize the service descriptor.
                    if( exists is List<ServiceDescriptor> l ) l.Add( d );
                    else externalMultiple[t] = new List<ServiceDescriptor>() { (ServiceDescriptor)exists, d };
                    return true;
                }
                externalMultiple.Add( t, d );
                return false;
            }
        }

        internal static Type GetImplementationType( ServiceDescriptor d )
        {
            if( d.ImplementationType != null )
            {
                return d.ImplementationType;
            }
            else if( d.ImplementationInstance != null )
            {
                return d.ImplementationInstance.GetType();
            }
            Type[]? typeArguments = d.ImplementationFactory.GetType().GenericTypeArguments;
            return typeArguments[1];
        }


    }
}
