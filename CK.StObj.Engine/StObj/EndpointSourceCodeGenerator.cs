using CK.CodeGen;
using System.Collections.Generic;
using System;
using System.Diagnostics;

namespace CK.Setup
{
    static class EndpointSourceCodeGenerator
    {
        // Always injected.
        const string _localNamespaces =
            """
            using CK.Core;
            using Microsoft.Extensions.DependencyInjection;
            using System.Collections.Generic;
            using System;
            using Microsoft.Extensions.DependencyInjection.Extensions;
            using System.Linq;
            """;

        // Always injected.
        const string _endpointTypeInternal =
            """
            interface IEndpointTypeInternal : IEndpointType
            {
                bool ConfigureServices( IActivityMonitor monitor, IStObjMap stObjMap, IServiceCollection commonEndpointContainer );
            }            
            """;

        // Injected only if there's at least one EndType.
        const string _endpointType =
            """
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
            """;

        // Injected only if there's at least one EndType.
        const string _endpointHelper =
            """
            static class EndpointHelper
            {
                enum SKind
                {
                    AtLeastOneSingleton = 1,
                    MultiSingleton = 2,
                    AtLeastOneScoped = 4
                }

                internal static IServiceCollection CreateCommonEndpointContainer( IActivityMonitor monitor,
                                                                                    IServiceCollection global,
                                                                                    Func<Type, bool> isEndpointService )
                {
                    var kindMap = new Dictionary<Type, SKind>();
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
                            if( kindMap.TryGetValue( t, out var kind ) )
                            {
                                // If multiple registrations exist, memorize it.
                                kindMap[t] = kind | SKind.MultiSingleton;
                            }
                            else
                            {
                                // Configure the relay to the last registered singleton.
                                endpoint.AddSingleton( t, sp => sp.GetRequiredService<EndpointTypeManager>().GlobalServiceProvider.GetService( t )! );
                                kindMap.Add( t, SKind.AtLeastOneSingleton );
                            }
                        }
                        else
                        {
                            // For scope, this is simple: we reuse the service descriptor instance.
                            endpoint.Add( d );
                            // But we flag this type as (at least) one scoped. If this type
                            // is also a singleton we'll have to handle its IEnumerable<T> registration
                            // as a scoped one instead of a singleton one.
                            if( kindMap.TryGetValue( t, out var kind ) )
                            {
                                kindMap[t] = kind | SKind.AtLeastOneScoped;
                            }
                            else
                            {
                                kindMap.Add( t, SKind.AtLeastOneScoped );
                            }
                        }
                    }
                    foreach( var (type, kind) in kindMap )
                    {
                        if( kind == (SKind.AtLeastOneSingleton | SKind.MultiSingleton) )
                        {
                            // If the type is registered as multiple singletons, we register
                            // the resolution of its IEnumerable<T> through the hook otherwise
                            // we'll have a enumeration of n times the last singleton registration.
                            var eType = typeof( IEnumerable<> ).MakeGenericType( type );
                            endpoint.AddSingleton( eType, sp => sp.GetRequiredService<EndpointTypeManager>().GlobalServiceProvider.GetService( eType )! );
                        }
                        else if( (kind & ~SKind.MultiSingleton) == (SKind.AtLeastOneSingleton | SKind.AtLeastOneScoped) )
                        {
                            // If the type is registered as a mix of scoped and singletons, this is the worst case.
                            // We cannot use the IEnumearble<T> resolution from the global hook since this
                            // would register scoped services in the root provider.
                            // We have no other choice than to fully resolve this here :(.
                            // This forces us to mimic the ServiceType to ImplementationType resolution of
                            // the Microsoft ServiceProvider implementation.
                            //
                            // This should barely happen, so, currently, we choose to not impact the regular case
                            // with a pre-registration of such hybrid cases: we process the collection again and
                            // emit a warning. If it happens that too many warnings occur in practice, this can be
                            // rewritten by storing the descriptor list in the kindMap (that would not be a "kind"
                            // map anymore but a reversed map of registrations).
                            using( monitor.OpenWarn( $"Type '{type:C}' is registered more than once with Scoped and Singleton lifetime. " +
                                                        $"Handling its hybrid 'IEnumerable<{type:C}>' registration is unfortunately required." ) )
                            {
                                RegisterResolvedEnumerable( monitor, global, endpoint, type );
                            }
                        }
                        // Else:
                        //  - if the type is registered only with AtLeastOneSingleton, we have nothing to do, the
                        //    enumerable will be resolved by whatever container with the single last registration.
                        //  - if the type is registered as scoped only, we let the container do its job as usual.
                    }
                    return endpoint;

                    static void RegisterResolvedEnumerable( IActivityMonitor monitor, IServiceCollection global, ServiceCollection endpoint, Type type )
                    {
                        string exceptionMessage = $"This hybrid Scoped/Singleton 'IEnumerable<{type.ToCSharpName()}>' from endpoint containers is invalid.";
                        string warnMessage = $"{exceptionMessage} Using it will throw an InvalidOperationException.";

                        // To avoid keeping the ServiceDecriptor in the closure and avoid resolving the implementation
                        // type each time, we compute 2 arrays of implementation types, one for the singletons (to be
                        // resolved by the global hook) and one for the scoped services.
                        var singTypes = new List<Type>();
                        var scopedTypes = new List<Type>();
                        bool hasError = false;
                        foreach( var d in global )
                        {
                            if( d.ServiceType == type )
                            {
                                var implType = GetImplementationType( d );
                                if( implType == type )
                                {
                                    hasError = true;
                                    monitor.Warn( $"Unable to analyze a mapped type. {warnMessage}" );
                                }
                                else if( d.Lifetime == ServiceLifetime.Singleton )
                                {
                                    if( singTypes.Contains( implType ) )
                                    {
                                        hasError = true;
                                        monitor.Warn( $"Duplicate mapping to '{implType:C}' (singleton). {warnMessage}" );
                                    }
                                    else
                                    {
                                        monitor.Info( $"=> '{implType:C}' (Singleton)" );
                                        singTypes.Add( implType );
                                    }
                                }
                                else
                                {
                                    if( scopedTypes.Contains( implType ) )
                                    {
                                        hasError = true;
                                        monitor.Warn( $"Duplicate mapping to '{implType:C}' (scoped). {warnMessage}" );
                                    }
                                    else
                                    {
                                        monitor.Info( $"=> '{implType:C}' (Scoped)" );
                                        scopedTypes.Add( implType );
                                    }
                                }
                            }
                        }
                        var aSing = singTypes.ToArray();
                        var aScop = scopedTypes.ToArray();

                        // Register the IEnumerable<> with its resolution.
                        var eType = typeof( IEnumerable<> ).MakeGenericType( type );
                        if( hasError )
                        {
                            endpoint.AddScoped( eType, sp => Throw.InvalidOperationException<object>( exceptionMessage ) );
                        }
                        else
                        {
                            endpoint.AddScoped( eType, sp =>
                            {
                                var a = Array.CreateInstance( type, aSing.Length + aScop.Length );
                                int i = 0;
                                var g = sp.GetRequiredService<EndpointTypeManager>().GlobalServiceProvider;
                                foreach( var t in aSing )
                                {
                                    a.SetValue( g.GetService( t ), i++ );
                                }
                                foreach( var t in aScop )
                                {
                                    a.SetValue( sp.GetService( t ), i++ );
                                }
                                return a;
                            } );
                        }
                    }

                    static Type GetImplementationType( ServiceDescriptor d )
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
            """;

        /// <summary>
        /// Always add the IEndpointTypeInternal (The EndpointTypeManager_CK needs it) but only
        /// add CK.StObj.EndpointType<TScopeData>, and the static EndpointHelper if at least one
        /// EndpointType exists.
        /// </summary>
        /// <param name="codeWorkspace">The code workspace.</param>
        /// <param name="hasEndpoint">Whether at least one endpoint exists.</param>
        internal static void GenerateSupportCode( ICodeWorkspace codeWorkspace, bool hasEndpoint )
        {
            var g = codeWorkspace.Global;
            g.Append( "namespace CK.StObj" )
             .OpenBlock()
             .Append( _localNamespaces )
             .Append( _endpointTypeInternal );
            if( hasEndpoint )
            {
                g.Append( _endpointType )
                 .Append( _endpointHelper );
            }
            g.CloseBlock();
        }

        public static void CreateFillMultipleEndpointMappingsMethod( ITypeScope rootType, IReadOnlyDictionary<Type, IMultipleInterfaceDescriptor> multipleMappings )
        {
            var fScope = rootType.GeneratedByComment().NewLine()
                                  .CreateFunction( "void FillMultipleEndpointMappings( IActivityMonitor monitor, Microsoft.Extensions.DependencyInjection.IServiceCollection commonEndpoint )" );
            foreach( var mapping in multipleMappings.Values )
            {
                Debug.Assert( mapping.ImplementationCount > 0 );
                if( mapping.IsScoped )
                {
                    // If all items are scoped, we can let the ServiceProvider implementation do its job (providing that we register
                    // the sp => sp.GetService( mapped ) for each mapping) or we can take control and provide an explicit registration.
                    // Since we have to do it for the hybrid case, let's always do it: this will avoid some reflection code from the
                    // ServiceProvider implementation.
                    fScope.Append( "commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( ", sp => {" ).NewLine()
                          .Append( "var a = Array.CreateInstance( " )
                          .AppendTypeOf( mapping.ItemType )
                          .Append( ", " ).Append( mapping.ImplementationCount ).Append( ");" ).NewLine();
                    int i = 0;
                    bool atLeastOneSingleton = false;
                    foreach( var impl in mapping.Implementations )
                    {
                        if( impl.IsScoped )
                        {
                            fScope.Append( "a.SetValue( sp.GetService( " ).AppendTypeOf( impl.ClassType ).Append( " ), " ).Append( i++ ).Append( " );" ).NewLine();
                        }
                        else
                        {
                            if( !atLeastOneSingleton )
                            {
                                fScope.Append( "var g = ((EndpointTypeManager)sp.GetService(typeof(EndpointTypeManager))).GlobalServiceProvider;" ).NewLine();
                                atLeastOneSingleton = true;
                            }
                            fScope.Append( "a.SetValue( g.GetService( " ).AppendTypeOf( impl.ClassType ).Append(" ), i++ ),").NewLine();
                        }
                    }
                    fScope.Append( "return a;" ).NewLine()
                          .Append( "}, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped ) );" );
                }
                else
                {
                    // For singletons, we register the resolution of its IEnumerable<T> through the hook otherwise
                    // we'll have a enumeration of n times the last singleton registration.
                    fScope.Append( "commonEndpoint.Add( new Microsoft.Extensions.DependencyInjection.ServiceDescriptor( " )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( ", sp => ((EndpointTypeManager)sp.GetService( typeof( EndpointTypeManager ) )).GlobalServiceProvider.GetService( " )
                          .AppendTypeOf( mapping.EnumerableType )
                          .Append( " ), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton ) );" ).NewLine();
                }
            }

        }

    }
}
