using CK.CodeGen;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using CK.Core;

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
            using System.Runtime.CompilerServices;
            using System.Diagnostics;
            using System.Diagnostics.CodeAnalysis;
            using System.Threading.Tasks;
            
            """;

        // Injected only if there's at least one EndpointType.
        const string _typedServiceDescriptor =
            """
            sealed class TypedServiceDescriptor : ServiceDescriptor
            {
                TypedServiceDescriptor( Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lt, Type implementationType )
                    : base( serviceType, factory, lt )
                {
                    ImplementationType = implementationType;
                }

                public new Type ImplementationType { get; }

                public static TypedServiceDescriptor Create( ServiceDescriptor o, Type implementationType )
                {
                    Debug.Assert( o.ImplementationInstance == null, "Instance singleton doesn't need this." );
                    Debug.Assert( o.ImplementationType == null, "Mapped type descriptor doesn't need this." );
                    Debug.Assert( o.ImplementationFactory != null );
                    return new TypedServiceDescriptor( o.ServiceType, o.ImplementationFactory, o.Lifetime, implementationType );
                }
            }
                        
            """;

        // Injected in EndpointHelper if there are endpoints.
        const string _mapping =
            """
            sealed class Mapping
            {
                object? _global;
                ServiceDescriptor? _lastGlobal;
                object? _endpoint;
                ServiceDescriptor? _lastEndpoint;
                bool _isScoped;
                public readonly bool IsAutoDI;
            
                public Mapping( bool isAutoDI, ServiceDescriptor? global, ServiceDescriptor? endpoint )
                {
                    _global = _lastGlobal = global;
                    _endpoint = _lastEndpoint = endpoint;
                    _isScoped = true;
                    IsAutoDI = isAutoDI;
                }

                public void AddGlobal( ServiceDescriptor d )
                {
                    Debug.Assert( _global != null );
                    if( _global is List<ServiceDescriptor> l ) l.Add( d );
                    else _global = new List<ServiceDescriptor>() { (ServiceDescriptor)_global, d };
                    _lastGlobal = d;
                }

                public void AddEndpoint( ServiceDescriptor d )
                {
                    if( _endpoint == null ) _endpoint = d;
                    else if( _endpoint is List<ServiceDescriptor> l ) l.Add( d );
                    else _endpoint = new List<ServiceDescriptor>() { (ServiceDescriptor)_endpoint, d };
                    _lastEndpoint = d;
                }

                public bool IsEmpty => _global == null && _endpoint == null;

                // We process the mapping only if there is at least 2 entries.
                // A single item uses the standard Service provider implementation.
                // Calling this clears the Endpoint: the mapping can be reused for the next
                // endpoint to configure.
                public bool HasMultiple( out object? endpoint )
                {
                    endpoint = _endpoint;
                    _endpoint = null;
                    _lastEndpoint = null;
                    // For global services, we registered the UniqueServiceDescriptor for our unique mappings when they come from
                    // the Automatic DI: we can skip them.
                    // For endpoint we are into te wild... as well as manual mapping that may have been done in the global container.
                    int nonUniqueGlobal = _global is List<ServiceDescriptor> desc ? desc.Count( d => d is not UniqueServiceDescriptor ) : 0;
                    if( nonUniqueGlobal > 1 ) return true;
                    return (_global != null && endpoint != null) || endpoint is List<ServiceDescriptor>;
                }

                public object? Global => _global;

                public ServiceDescriptor? LastGlobal => _lastGlobal;

                public ServiceDescriptor? LastEndpoint => _lastEndpoint;

                public object? Endpoint => _endpoint;

                // True by default: if the IEnumerable can be singleton, it will be.
                // When LockAsSingleton() is called this is definitely false and no Scoped
                // services can appear in the IEnumerable: this is used when a [IsMultiple] 
                // multiple has been resolved to a singleton.
                public bool IsScoped => _isScoped;

                public void LockAsSingleton() => _isScoped = false;
            }
            
            """;

        // Injected in EndpointHelper if there's no endpoint.
        const string _endpointTypeInternalNoEndpoint =
            """
            interface IDIContainerInternal : IDIContainer {}
            """;

        const string _checkAndNormalizeAmbientServices =
            """
            internal static bool CheckAndNormalizeAmbientServices( IActivityMonitor monitor, IServiceCollection services, bool isFrontEndpoint )
            {
                bool success = true;
                var firstResolutions = new ServiceDescriptor?[DIContainerHub_CK._ambientMappings.Length];
                foreach( var d in services )
                {
                    if( d.ServiceType == typeof( DIContainerHub ) || d.ServiceType == typeof( AmbientServiceHub ) )
                    {
                        monitor.Error( $"Service '{d.ServiceType.Name}' must not be configured." );
                        success = false;
                    }
                    else
                    {
                        int idx = DIContainerHub_CK._ambientMappings.IndexOf( m => m.AmbientServiceType == d.ServiceType );
                        if( idx >= 0 )
                        {
                            if( firstResolutions[idx] == null )
                            {
                                firstResolutions[idx] = d;
                            }
                            else
                            {
                                monitor.Error( $"Ambient service '{d.ServiceType.Name}' is mapped more than once. Ambient service cannot be added more than once in a DI container." );
                                success = false;
                            }
                        }
                    }
                }
                if( !success ) return false;
                foreach( var (idx, m, other) in GetMappingsRange() )
                {
                    Func<IServiceProvider, object>? first = null;
                    for( int i = idx; i < other; ++i )
                    {
                        var candidate = firstResolutions[i];
                        if( candidate == null )
                        {
                            if( first != null )
                            {
                                services.AddScoped( DIContainerHub_CK._ambientMappings[i].AmbientServiceType, first );
                            }
                        }
                        else
                        {
                            if( first == null )
                            {
                                first = sp => sp.GetService( candidate.ServiceType )!;
                                for( var before = i - 1; before >= idx; before-- )
                                {
                                    if( firstResolutions[before] == null )
                                    {
                                        services.AddScoped( DIContainerHub_CK._ambientMappings[before].AmbientServiceType, first );
                                    }
                                }
                            }
                        }
                    }
                    if( first == null )
                    {
                        var defaults = isFrontEndpoint
                                        ? DIContainerHub_CK._ambientServiceEndpointDescriptors
                                        : DIContainerHub_CK._ambientServiceBackendDescriptors;
                        services.AddRange( defaults.Skip( idx ).Take( other - idx ) );
                    }
                }
                // Intrinsic AmbientServiceHub.
                if( isFrontEndpoint )
                {
                    services.Add( new ServiceDescriptor( typeof( AmbientServiceHub ), sp => new AmbientServiceHub_CK( sp ), ServiceLifetime.Scoped ) );
                }
                else
                {
                    services.Add( new ServiceDescriptor( typeof( AmbientServiceHub ), ScopeDataHolder.GetAmbientServiceHub, ServiceLifetime.Scoped ) );
                }
                return true;

                static IEnumerable<(int, DIContainerHub.AmbientServiceMapping, int)> GetMappingsRange()
                {
                    int len = DIContainerHub_CK._ambientMappings.Length;
                    for( int i = 0; i < len; )
                    {
                        var m = DIContainerHub_CK._ambientMappings[i];
                        int start = i;
                        ++i;
                        while( i < len && DIContainerHub_CK._ambientMappings[i].MappingIndex == m.MappingIndex )
                        {
                            ++i;
                        }
                        yield return (start, m, i);
                    }
                }
            }
                        
            """;

        // Injected in EndpointHelper if there's no endpoint.
        const string _fillStObjMappingsNoEndpoint =
            """
            internal static void FillStObjMappingsNoEndpoint( IActivityMonitor monitor,
                                                                IStObjMap stObjMap,
                                                                IServiceCollection global )
            {
                // We have no real issues for real objects: we simply create singleton descriptors
                // with the true singleton instance and add them to the global container.
                foreach( IStObjFinalImplementation o in stObjMap.StObjs.FinalImplementations )
                {
                    var typeMapping = new ServiceDescriptor( o.ClassType, o.Implementation );
                    global.Add( typeMapping );
                    foreach( var unique in o.UniqueMappings )
                    {
                        var uMapping = new UniqueServiceDescriptor( unique, o.Implementation );
                        global.Add( uMapping );
                    }
                    foreach( var multi in o.MultipleMappings )
                    {
                        var mMapping = new ServiceDescriptor( multi, o.Implementation );
                        global.Add( mMapping );
                    }
                }
                foreach( IStObjServiceClassDescriptor s in stObjMap.Services.MappingList )
                {
                    if( s.IsScoped )
                    {
                        if( (s.AutoServiceKind & AutoServiceKind.IsAmbientService) == 0 )
                        {
                            AddGlobalServiceMapping( global, s, ServiceLifetime.Scoped );
                        }
                    }
                    else
                    {
                        if( s.ClassType == typeof( DIContainerHub ) ) continue;
                        AddGlobalServiceMapping( global, s, ServiceLifetime.Singleton );
                    }
                }
            }
            
            """;

        // Always injected.
        const string _uniqueServiceDescriptor =
            """
            sealed class UniqueServiceDescriptor : ServiceDescriptor
            {
                // For Real objects
                public UniqueServiceDescriptor( Type serviceType, object instance )
                    : base( serviceType, instance )
                {
                }

                // For services.
                public UniqueServiceDescriptor( Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime )
                    : base( serviceType, factory, lifetime )
                {
                }
            }
            """;

        // Always injected.
        const string _addGlobalServiceMapping =
            """
            static void AddGlobalServiceMapping( IServiceCollection global, IStObjServiceClassDescriptor s, ServiceLifetime lt )
            {
                var typeMapping = new ServiceDescriptor( s.ClassType, s.FinalType, lt );
                global.Add( typeMapping );
                // Same delegate used for all the mappings (if any). 
                Func<IServiceProvider, object>? shared = null;
                foreach( var unique in s.UniqueMappings )
                {
                    var uMapping = new UniqueServiceDescriptor( unique, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
                    global.Add( uMapping );
                }
                foreach( var multi in s.MultipleMappings )
                {
                    var mMapping = new ServiceDescriptor( multi, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
                    global.Add( mMapping );
                }
            }
            
            """;

        // Injected in EndpointHelper if there are endpoints.
        const string _endpointTypeInternalWithEndpoints =
            """
            interface IDIContainerInternal : IDIContainer
            {
                bool ConfigureServices( IActivityMonitor monitor,
                                        IStObjMap stObjMap,
                                        Dictionary<Type,Mapping> mappings,
                                        ServiceDescriptor[] trueSingletons );
            }
            """;

        // Injected in EndpointHelper if there are endpoints.
        const string _fillStObjMappingsWithEndpoints =
            """
            internal static void FillStObjMappingsWithEndpoints( IActivityMonitor monitor,
                                                                    IStObjMap stObjMap,
                                                                    IServiceCollection global,
                                                                    Dictionary<Type, Mapping> mappings )
            {
                // We have no real issues for real objects: we simply create singleton descriptors
                // with the true singleton instance and add them to the global container and
                // to the endpoint mappings.
                foreach( IStObjFinalImplementation o in stObjMap.StObjs.FinalImplementations )
                {
                    var typeMapping = new ServiceDescriptor( o.ClassType, o.Implementation );
                    global.Add( typeMapping );
                    Mapping? m = new Mapping( true, typeMapping, null );
                    // Use Add: no external configuration must register a IRealObject.
                    mappings.Add( o.ClassType, m );
                    foreach( var unique in o.UniqueMappings )
                    {
                        var uMapping = new UniqueServiceDescriptor( unique, o.Implementation );
                        global.Add( uMapping );
                        m.AddGlobal( uMapping );
                    }
                    foreach( var multi in o.MultipleMappings )
                    {
                        var mMapping = new ServiceDescriptor( multi, o.Implementation );
                        global.Add( mMapping );
                        if( mappings.TryGetValue( multi, out var mm ) )
                        {
                            mm.AddGlobal( mMapping );
                        }
                        else
                        {
                            mappings.Add( multi, new Mapping( false, mMapping, null ) );
                        }
                    }
                }
                // For services it's less trivial: the mappings must be able to resolve the descriptor's implementation type
                // so that multiple can be handled.
                // One way would be to create a typed lambda where sp => sp.GetService( s.ClassType ) is used
                // so that the returned type of Func<IServiceProvider,s.ClassType> can be inspected.
                // The other one introduces the TypedServiceDescriptor : ServiceDescriptor specialization that
                // capture the implementation type.
                foreach( var s in stObjMap.Services.MappingList )
                {
                    bool isEndpointService = (s.AutoServiceKind & AutoServiceKind.IsEndpointService) != 0;
                    if( s.IsScoped )
                    {
                        if( isEndpointService )
                        {
                            if( (s.AutoServiceKind & AutoServiceKind.IsAmbientService) == 0 )
                            {
                                AddGlobalServiceMapping( global, s, ServiceLifetime.Scoped );
                            }
                        }
                        else
                        {
                            AddServiceMapping( global, mappings, s, ServiceLifetime.Scoped );
                        }
                    }
                    else
                    {
                        if( s.ClassType == typeof( DIContainerHub ) ) continue;
                        if( isEndpointService )
                        {
                            AddGlobalServiceMapping( global, s, ServiceLifetime.Singleton );
                        }
                        else
                        {
                            AddServiceMapping( global, mappings, s, ServiceLifetime.Singleton );
                        }
                    }
                }
                // Locking the IsMultiple optimized to be singleton: this prevents
                // any multiple registration of the type with a scope lifetime.
                // If it happens (either by global configuration or by a endpoint configuration),
                // the StObjMap registration fails.
                foreach( var multiple in stObjMap.MultipleMappings.Values )
                {
                    if( !multiple.IsScoped ) mappings[multiple.ItemType].LockAsSingleton();
                }

                static void AddServiceMapping( IServiceCollection global,
                                                Dictionary<Type, Mapping> mappings,
                                                IStObjServiceClassDescriptor s,
                                                ServiceLifetime lt )
                {
                    var typeMapping = new ServiceDescriptor( s.ClassType, s.FinalType, lt );
                    global.Add( typeMapping );

                    Mapping m = new Mapping( true, typeMapping, null );
                    mappings.Add( s.ClassType, m );
                    // Same delegate used for all the mappings (if any). 
                    Func<IServiceProvider, object>? shared = null;
                    foreach( var unique in s.UniqueMappings )
                    {
                        var uMapping = new UniqueServiceDescriptor( unique, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
                        global.Add( uMapping );
                        // We don't need a TypedServiceDescriptor here: this is a unique mapping, no
                        // multiple is allowed by design.
                        m.AddGlobal( uMapping );
                    }
                    foreach( var multi in s.MultipleMappings )
                    {
                        var mMapping = new ServiceDescriptor( multi, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
                        global.Add( mMapping );
                        mMapping = TypedServiceDescriptor.Create( mMapping, s.ClassType );
                        if( mappings.TryGetValue( multi, out var mm ) )
                        {
                            mm.AddGlobal( mMapping );
                        }
                        else
                        {
                            mappings.Add( multi, new Mapping( false, mMapping, null ) );
                        }
                    }
                }
            }
            
            """;

        // Injected in EndpointHelper if there are endpoints.
        const string _createInitialMapping =
            """
            internal static Dictionary<Type, Mapping> CreateInitialMapping( IActivityMonitor monitor,
                                                                            IServiceCollection global,
                                                                            Func<Type, bool> isEndpointService )
            {
                Dictionary<Type, Mapping> mappings = new Dictionary<Type, Mapping>();
                foreach( var d in global )
                {
                    var t = d.ServiceType;
                    // Skip any endpoint service and IHostedService.
                    // There's no need to have the IHostedService multiple service in any other container than the global one.
                    if( isEndpointService( t ) || t == typeof( Microsoft.Extensions.Hosting.IHostedService ) )
                    {
                        continue;
                    }
                    if( mappings.TryGetValue( t, out var exists ) )
                    {
                        exists.AddGlobal( d );
                    }
                    else
                    {
                        mappings.Add( t, new Mapping( false, d, null ) );
                    }
                }
                return mappings;
            }
            
            """;

        // Injected only if there are endpoints.
        const string _scopedDataHolder =
            """
            sealed class ScopeDataHolder
            {
                [AllowNull]
                internal DIContainerDefinition.IScopedData _data;

                internal static object GetAmbientServiceHub( IServiceProvider sp )
                {
                    // See below.
                    return Unsafe.As<DIContainerDefinition.BackendScopedData>( Unsafe.As<ScopeDataHolder>( sp.GetService( typeof( ScopeDataHolder ) )! )._data ).AmbientServiceHub;
                }

                internal static object GetAmbientService( IServiceProvider sp, int index )
                {
                    // This looks scary, but:
                    // - first we resolve the ScopeDataHolder type that is necessary a ScopeDataHolder.
                    // - then we know that the _data is necessarily a BackendScopedData because:
                    //      - this method is called only for backend contexts (front endpoints use the registered services or
                    //        the IEndpointUbiquitousServiceDefault<> singletons to resolve missing Ambient services).
                    //      - the BackendScopedData inheritance is checked at setup time for Backend contexts.
                    // - We can then access the AmbientServiceHub instance that is the code generated class with its AmbientServiceHub_CK.At( mappingIndex )
                    //   hidden accessor.
                    return Unsafe.As<AmbientServiceHub_CK>( Unsafe.As<DIContainerDefinition.BackendScopedData>( Unsafe.As<ScopeDataHolder>( sp.GetService( typeof( ScopeDataHolder ) )! )._data).AmbientServiceHub ).At( index );
                }
            }
            
            """;

        // Used by EndpointType. Injected only if there are endpoints.
        const string _globalServices = """
            sealed class GlobalServiceExists : IServiceProviderIsService
            {
                readonly IReadOnlyDictionary<Type, Mapping> _externalMappings;

                public GlobalServiceExists( IReadOnlyDictionary<Type, Mapping> externalMappings )
                {
                    _externalMappings = externalMappings;
                }

                public bool IsService( Type serviceType ) => _externalMappings.TryGetValue( serviceType, out var m ) && !m.IsEmpty;
            }
            
            """;

        // Injected only if there are endpoints.
        const string _diContainerType =
            """
            [CK.Core.StObjGen]
            sealed class DIContainer<TScopedData> : IDIContainer<TScopedData>, IDIContainerInternal where TScopedData : class, DIContainerDefinition.IScopedData
            {
                IDIContainerServiceProvider<TScopedData>? _services;

                readonly DIContainerDefinition<TScopedData> _definition;
                internal ServiceCollection? _configuration;
                Type[] _singletons;
                Type[] _scoped;
                readonly object _lock;
                bool _initializationSuccess;

                public DIContainer( DIContainerDefinition<TScopedData> definition )
                {
                    _singletons = _scoped = Type.EmptyTypes;
                    _definition = definition;
                    _lock = new object();
                }

                public DIContainerDefinition DIContainerDefinition => _definition;

                public Type ScopeDataType => typeof( TScopedData );

                public string Name => _definition.Name;

                public IDIContainerServiceProvider<TScopedData> GetContainer() => _services ?? DoCreateContainer();

                public bool IsService( Type serviceType ) => GetContainer().IsService( serviceType );

                public IReadOnlyCollection<Type> SpecificSingletonServices => _singletons;

                public IReadOnlyCollection<Type> SpecificScopedServices => _scoped;

                IDIContainerServiceProvider<TScopedData> DoCreateContainer()
                {
                    lock( _lock )
                    {
                        if( _services == null )
                        {
                            if( !_initializationSuccess ) Throw.InvalidOperationException( "Endpoint initialization failed. It cannot be used." );
                            Debug.Assert( _configuration != null );
                            _services = new Provider( _configuration.BuildServiceProvider() );
                            // Release the configuration now that the endpoint container is built.
                            _configuration = null;
                        }
                        return _services;
                    }
                }

                sealed class Provider : IDIContainerServiceProvider<TScopedData>
                {
                    readonly ServiceProvider _serviceProvider;
                    IServiceProviderIsService? _serviceProviderIsService;

                    public Provider( ServiceProvider serviceProvider )
                    {
                        _serviceProvider = serviceProvider;
                    }

                    public AsyncServiceScope CreateAsyncScope( TScopedData scopedData )
                    {
                        var scope = _serviceProvider.CreateAsyncScope();
                        if( scopedData is DIContainerDefinition.BackendScopedData back ) back.AmbientServiceHub.Lock();
                        scope.ServiceProvider.GetRequiredService<ScopeDataHolder>()._data = scopedData;
                        return scope;
                    }

                    public IServiceScope CreateScope( TScopedData scopedData )
                    {
                        var scope = _serviceProvider.CreateScope();
                        if( scopedData is DIContainerDefinition.BackendScopedData back ) back.AmbientServiceHub.Lock();
                        scope.ServiceProvider.GetRequiredService<ScopeDataHolder>()._data = scopedData;
                        return scope;
                    }

                    public object? GetService( Type serviceType ) => _serviceProvider.GetService( serviceType );

                    public void Dispose() => _serviceProvider.Dispose();

                    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

                    public bool IsService( Type serviceType )
                    {
                        return (_serviceProviderIsService ??= _serviceProvider.GetRequiredService<IServiceProviderIsService>()).IsService( serviceType );
                    }
                }

                static TScopedData GetScopedData( IServiceProvider sp )
                {
                    return Unsafe.As<TScopedData>( Unsafe.As<ScopeDataHolder>( sp.GetService( typeof( ScopeDataHolder ) )! )._data );
                }

                public bool ConfigureServices( IActivityMonitor monitor,
                                                IStObjMap stObjMap,
                                                Dictionary<Type, Mapping> mappings,
                                                ServiceDescriptor[] commonDescriptors )
                {
                    var endpoint = new ServiceCollection();

                    // Calls the user configuration.
                    _definition.ConfigureEndpointServices( endpoint, GetScopedData, new GlobalServiceExists( mappings ) );
                    // Normalizes ubiquitous services.
                    if( !EndpointHelper.CheckAndNormalizeAmbientServices( monitor, endpoint, _definition.Kind == DIContainerKind.Endpoint ) )
                    {
                        return false;
                    }
                    // Process the endpoint specific registrations to detect extra registrations: there must not be any type
                    // mapped to IRealObject or mapping to IAutoService for any service not declared as a EndpointService.
                    // This also updates the mappings with potential Mapping.Endpoint objects.
                    if( CheckRegistrations( monitor,
                                            endpoint,
                                            stObjMap,
                                            mappings ) )
                    {
                        var configuration = new ServiceCollection();
                        // Generates the Multiple descriptors.
                        var builder = new FinalConfigurationBuilder( _definition.Name, mappings );
                        builder.FinalConfigure( monitor, configuration );
                        // Add the scoped ScopeDataHolder and the true singletons StObjMap, DIContainerHub, all the
                        // IDIContainer<TScopeData> and the IEnumerable<IDIContainer>.
                        configuration.AddRange( commonDescriptors );
                        // Waiting for .Net 8.
                        // configuration.MakeReadOnly();
                        _configuration = configuration;

                        return _initializationSuccess = true;
                    }
                    return false;

                    bool CheckRegistrations( IActivityMonitor monitor,
                                                ServiceCollection configuration,
                                                IStObjMap stObjMap,
                                                Dictionary<Type, Mapping> mappings )
                    {
                        List<Type>? singletons = null;
                        List<Type>? scoped = null;
                        bool success = true;
                        foreach( var d in configuration )
                        {
                            if( mappings.TryGetValue( d.ServiceType, out var exists ) )
                            {
                                exists.AddEndpoint( d );
                            }
                            else mappings.Add( d.ServiceType, new Mapping( false, null, d ) );

                            bool isAmbientService = DIContainerHub_CK._ambientMappings.Any( uD => uD.AmbientServiceType == d.ServiceType );
                            if( !isAmbientService )
                            {
                                if( d.Lifetime == ServiceLifetime.Singleton )
                                {
                                    singletons ??= new List<Type>();
                                    singletons.Add( d.ServiceType );
                                }
                                else
                                {
                                    scoped ??= new List<Type>();
                                    scoped.Add( d.ServiceType );
                                }
                            }
                        }
                        if( !ErrorNotEndpointAutoServices( monitor, _definition, stObjMap, singletons, ServiceLifetime.Singleton ) ) success = false;
                        if( !ErrorNotEndpointAutoServices( monitor, _definition, stObjMap, scoped, ServiceLifetime.Scoped ) ) success = false;
                        if( scoped != null ) _scoped = scoped.ToArray();
                        if( singletons != null ) _singletons = singletons.ToArray();
                        return success;

                        static bool ErrorNotEndpointAutoServices( IActivityMonitor monitor, DIContainerDefinition definition, IStObjMap stObjMap, List<Type>? extra, ServiceLifetime lt )
                        {
                            bool success = true;
                            if( extra != null )
                            {
                                foreach( var s in extra )
                                {
                                    var autoMap = stObjMap.ToLeaf( s );
                                    if( autoMap != null )
                                    {
                                        if( autoMap is IStObjServiceClassDescriptor service )
                                        {
                                            if( (service.AutoServiceKind & AutoServiceKind.IsEndpointService) == 0 )
                                            {
                                                monitor.Error( $"Endpoint '{definition.Name}' cannot configure the {lt} '{s:C}': it is a {(autoMap.IsScoped ? "Scoped" : "Singleton")} automatic service mapped to '{autoMap.ClassType:C}' that is not declared to be a Endpoint service." );
                                                success = false;
                                            }
                                        }
                                        else
                                        {
                                            monitor.Error( $"Endpoint '{definition.Name}' cannot configure the {lt} '{s:C}': it is mapped to the real object '{autoMap.ClassType:C}'." );
                                            success = false;
                                        }
                                    }
                                    else
                                    {
                                        // Silently skips IActivityMonitor and IParallelLogger that are "by design".
                                        if( s != typeof( IActivityMonitor ) && s != typeof( IParallelLogger ) )
                                        {
                                            monitor.Warn( $"Endpoint '{definition.Name}' supports the {lt} service '{s:C}' that is not declared as a endpoint service." );
                                        }
                                    }
                                }
                            }
                            return success;
                        }
                    }
                }
            }
            
            """;

        // Injected only if there are endpoints.
        const string _finalConfigurationBuilder =
            """
            readonly struct FinalConfigurationBuilder
            {
                readonly List<Type> _singGlobal;
                readonly List<Type> _singLocal;
                readonly List<object> _singInst;
                readonly List<Type> _scopTypes;
                readonly List<string> _typeMappedErrors;
                readonly string _endpointName;
                readonly IReadOnlyDictionary<Type, Mapping> _mappings;

                public FinalConfigurationBuilder( string endpointName, IReadOnlyDictionary<Type, Mapping> mappings )
                {
                    _endpointName = endpointName;
                    _mappings = mappings;
                    _singGlobal = new List<Type>();
                    _singLocal = new List<Type>();
                    _singInst = new List<object>();
                    _scopTypes = new List<Type>();
                    _typeMappedErrors = new List<string>();
                }

                record struct MultipleInfo( bool IsPureGlobalSingleton,
                                            Type[]? SingGlobal,
                                            Type[]? SingLocal,
                                            object[]? SingInst,
                                            Type[]? ScopTypes,
                                            int Count );

                // Final transfers of the registrations from the computed mappings to the endpoint configuration.
                // Singletons with no provided instance (they have a factory) are rerouted to the global
                // containers. Singletons with an instance or open generics (like IOptions<>) are copied
                // as-is: open generics requires the ImplementationType to be set (otherwise the
                // CallSiteFactory.Populate() throws). This is not a concern since no instance are
                // realized for an open generic (this check is for security).
                // Note that we register only the LAST seen instance AND its corresponding IEnumerable<T>
                // if multiple registrations appears.
                public void FinalConfigure( IActivityMonitor monitor, ServiceCollection endpoint )
                {
                    foreach( var (t, m) in _mappings )
                    {
                        if( m.IsAutoDI )
                        {
                            Debug.Assert( m.Global != null && m.Endpoint == null );
                            var reg = m.Global;
                            if( reg is List<ServiceDescriptor> list )
                            {
                                foreach( var d in list )
                                {
                                    AddFromAutoDI( endpoint, d );
                                }
                            }
                            else
                            {
                                AddFromAutoDI( endpoint, (ServiceDescriptor)reg );
                            }

                            static void AddFromAutoDI( ServiceCollection endpoint, ServiceDescriptor d )
                            {
                                if( d.Lifetime == ServiceLifetime.Singleton
                                    && d.ImplementationInstance == null
                                    && !d.ServiceType.IsGenericTypeDefinition )
                                {
                                    endpoint.Add( new ServiceDescriptor( d.ServiceType,
                                                                         sp => DIContainerHub_CK.GlobalServices.GetService( d.ServiceType )!,
                                                                         ServiceLifetime.Singleton ) );
                                }
                                else
                                {
                                    endpoint.Add( d );
                                }
                            }
                        }
                        else
                        {
                            var last = m.LastEndpoint ?? m.LastGlobal;
                            if( last == null ) continue;
                            if( m.LastEndpoint == null
                                && last.Lifetime == ServiceLifetime.Singleton
                                && last.ImplementationInstance == null
                                && !last.ServiceType.IsGenericTypeDefinition )
                            {
                                endpoint.Add( new ServiceDescriptor( t, sp => DIContainerHub_CK.GlobalServices.GetService( t )!, ServiceLifetime.Singleton ) );
                            }
                            else
                            {
                                endpoint.Add( last );
                            }
                        }

                        if( !m.HasMultiple( out var mappingEndpoint ) ) continue;

                        var r = CreateMultipleInfo( monitor, t, m.IsScoped, m.Global, mappingEndpoint );

                        var tEnum = typeof( IEnumerable<> ).MakeGenericType( t );
                        if( r.ScopTypes != null )
                        {
                            if( !AddErrorEndpoint( monitor, endpoint, tEnum, t, ServiceLifetime.Scoped ) )
                            {
                                endpoint.Add( new ServiceDescriptor( tEnum,
                                    sp =>
                                    {
                                        var a = Array.CreateInstance( t, r.Count );
                                        int i = 0;
                                        foreach( var scop in r.ScopTypes )
                                        {
                                            a.SetValue( sp.GetService( scop ), i++ );
                                        }
                                        if( r.SingInst != null )
                                        {
                                            foreach( var inst in r.SingInst )
                                            {
                                                a.SetValue( inst, i++ );
                                            }
                                        }
                                        if( r.SingLocal != null )
                                        {
                                            foreach( var sing in r.SingLocal )
                                            {
                                                a.SetValue( sp.GetService( sing ), i++ );
                                            }
                                        }
                                        if( r.SingGlobal != null )
                                        {
                                            var g = DIContainerHub_CK.GlobalServices;
                                            foreach( var sing in r.SingGlobal )
                                            {
                                                a.SetValue( g.GetService( sing ), i++ );
                                            }
                                        }
                                        return a;
                                    }, ServiceLifetime.Scoped ) );
                            }
                        }
                        else
                        {
                            if( r.IsPureGlobalSingleton )
                            {
                                // For homogeneous singletons from global, we register the resolution of its IEnumerable<T>
                                // through the hook otherwise we'll have a enumeration of n times the last singleton registration.
                                endpoint.Add( new ServiceDescriptor( tEnum, sp => DIContainerHub_CK.GlobalServices.GetService( tEnum )!, ServiceLifetime.Singleton ) );
                            }
                            else
                            {
                                if( !AddErrorEndpoint( monitor, endpoint, tEnum, t, ServiceLifetime.Singleton ) )
                                {
                                    endpoint.Add( new ServiceDescriptor( tEnum,
                                        sp =>
                                        {
                                            var a = Array.CreateInstance( t, r.Count );
                                            int i = 0;
                                            if( r.SingInst != null )
                                            {
                                                foreach( var inst in r.SingInst )
                                                {
                                                    a.SetValue( inst, i++ );
                                                }
                                            }
                                            if( r.SingLocal != null )
                                            {
                                                foreach( var sing in r.SingLocal )
                                                {
                                                    a.SetValue( sp.GetService( sing ), i++ );
                                                }
                                            }
                                            if( r.SingGlobal != null )
                                            {
                                                var g = DIContainerHub_CK.GlobalServices;
                                                foreach( var sing in r.SingGlobal )
                                                {
                                                    a.SetValue( g.GetService( sing ), i++ );
                                                }
                                            }
                                            return a;
                                        }, ServiceLifetime.Singleton ) );
                                }
                            }
                        }
                    }
                }

                /// <summary>
                /// When true is returned, this emits only warnings: this does not prevent the StObjMap to
                /// be registered: only if the IEnumerable is resolved, a InvalidOperationException is raised.
                /// </summary>
                bool AddErrorEndpoint( IActivityMonitor monitor, ServiceCollection endpoint, Type tEnum, Type tItem, ServiceLifetime lt )
                {
                    var exceptionMessage = HandleTypeMappedErrors( monitor, tItem );
                    if( exceptionMessage != null )
                    {
                        endpoint.Add( new ServiceDescriptor( tEnum, sp => Throw.InvalidOperationException<object>( exceptionMessage ), lt ) );
                        return true;
                    }
                    return false;
                }

                string? HandleTypeMappedErrors( IActivityMonitor monitor, Type t )
                {
                    string? exceptionMessage = null;
                    if( _typeMappedErrors.Count > 0 )
                    {
                        exceptionMessage = $"The 'IEnumerable<{t.ToCSharpName()}>' cannot be used from endpoint '{_endpointName}'.";
                        using( monitor.OpenWarn( $"{exceptionMessage} Using it will throw a InvalidOperationException." ) )
                        {
                            foreach( var e in _typeMappedErrors ) monitor.Warn( e );
                        }
                    }
                    return exceptionMessage;
                }

                MultipleInfo CreateMultipleInfo( IActivityMonitor monitor, Type t, bool isScoped, object? global, object? endpoint )
                {
                    _singGlobal.Clear();
                    _singLocal.Clear();
                    _scopTypes.Clear();
                    _typeMappedErrors.Clear();
                    if( global != null )
                    {
                        Handle( global, _singGlobal, _singInst, _scopTypes, _typeMappedErrors );
                    }
                    if( endpoint != null )
                    {
                        Handle( endpoint, _singLocal, _singInst, _scopTypes, _typeMappedErrors );
                    }
                    if( !isScoped && _singLocal.Count == 0 && _scopTypes.Count == 0 )
                    {
                        return new MultipleInfo( true, null, null, null, null, _singGlobal.Count + _singInst.Count );
                    }
                    int count = 0;
                    Type[]? singGlobal = null;
                    if( _singGlobal.Count > 0 )
                    {
                        singGlobal = _singGlobal.ToArray();
                        count = singGlobal.Length;
                    }
                    Type[]? singLocal = null;
                    if( _singLocal.Count > 0 )
                    {
                        singLocal = _singLocal.ToArray();
                        CheckRecursiveTypeMapping( t, singLocal, ServiceLifetime.Singleton );
                        count += singLocal.Length;
                    }
                    object[]? singInst = null;
                    if( _singInst.Count > 0 )
                    {
                        singInst = _singInst.ToArray();
                        count += singInst.Length;
                    }
                    Type[]? scopTypes = null;
                    if( _scopTypes.Count > 0 )
                    {
                        if( !isScoped )
                        {
                            // The [IsMultiple] has been resolved as a singleton. It cannot contain a scope: this is an
                            // error that prevent the StObjMap to be registered, we use monitor.Fatal to signal this fatal
                            // error.
                            monitor.Fatal( $"The IEnumerable<{t:C}> of [IsMultiple] is a Singleton that contains externally defined Scoped mappings (endpoint '{_endpointName}'): " +
                                            $"'{_scopTypes.Select( t => t.ToCSharpName() ).Concatenate( "', '" )}'." );
                            // Don't bother here: we return an empty result.
                            return new MultipleInfo( false, null, null, null, null, 0 );
                        }
                        scopTypes = _scopTypes.ToArray();
                        CheckRecursiveTypeMapping( t, scopTypes, ServiceLifetime.Scoped );
                        count += scopTypes.Length;
                    }

                    return new MultipleInfo( false, singGlobal, singLocal, singInst, scopTypes, count );

                    static void Handle( object o, List<Type> singType, List<object> singInst, List<Type> scopTypes, List<string> typeMappedErrors )
                    {
                        if( o is List<ServiceDescriptor> l )
                        {
                            foreach( var d in l ) Handle( d, singType, singInst, scopTypes, typeMappedErrors );
                        }
                        else Handle( (ServiceDescriptor)o, singType, singInst, scopTypes, typeMappedErrors );

                        static void Handle( ServiceDescriptor ext, List<Type> singTypes, List<object> singInst, List<Type> scopTypes, List<string> typeMappedErrors )
                        {
                            if( ext is UniqueServiceDescriptor ) return;
                            if( ext.Lifetime == ServiceLifetime.Singleton )
                            {
                                if( ext.ImplementationInstance != null )
                                {
                                    singInst.Add( ext.ImplementationInstance );
                                }
                                else
                                {
                                    HandleImplementationType( ext, ServiceLifetime.Singleton, singTypes, typeMappedErrors );
                                }
                            }
                            else
                            {
                                HandleImplementationType( ext, ServiceLifetime.Scoped, scopTypes, typeMappedErrors );
                            }

                            static void HandleImplementationType( ServiceDescriptor d,
                                                                    ServiceLifetime lt,
                                                                    List<Type> list,
                                                                    List<string> mappedTypeError )
                            {
                                var implType = GetImplementationType( d );
                                if( implType == typeof( object ) )
                                {
                                    mappedTypeError.Add( $"Unable to analyze {lt} '{d.ServiceType.ToCSharpName()}' type: its registration doesn't capture the target implementation type." );
                                }
                                else if( list.Contains( implType ) )
                                {
                                    mappedTypeError.Add( $"Duplicate mapping from {lt} '{d.ServiceType.ToCSharpName()}' to '{implType:C}' type." );
                                }
                                else
                                {
                                    list.Add( implType );
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
                                    else if( d is TypedServiceDescriptor dT )
                                    {
                                        return dT.ImplementationType;
                                    }
                                    Type[]? typeArguments = d.ImplementationFactory!.GetType().GenericTypeArguments;
                                    return typeArguments[1];
                                }
                            }
                        }
                    }

                }

                // Check that a type is not mapped to itself (can be a lack of explicit implementation type mapping).
                // This must not appear in endpoint singletons (singLocal) nor in scoped (scopTypes) since the
                // lambda sp => sp.GetService( t ) is used.
                void CheckRecursiveTypeMapping( Type t, Type[] endpointMappedTypes, ServiceLifetime lt )
                {
                    if( Array.IndexOf( endpointMappedTypes, t ) != -1 )
                    {
                        _typeMappedErrors.Add( $"Mapping {lt} '{t.ToCSharpName()}' to itself detected (its registration may not capture the target implementation type)." );
                    }
                }
            }
            
            """;

        /// <summary>
        /// Always generate the IDIContainerInternal interface code (The DIContainerHub_CK needs it)
        /// and TypedServiceDescriptor code (the FillStObjMappings function needs it) but only
        /// add CK.StObj.EndpointType&lt;TScopeData&gt;, and the static EndpointHelper if at least one
        /// EndpointType exists.
        /// </summary>
        /// <param name="codeWorkspace">The code workspace.</param>
        /// <param name="hasEndpoint">Whether at least one endpoint exists.</param>
        internal static void GenerateSupportCode( ICodeWorkspace codeWorkspace, bool hasEndpoint )
        {
            var g = codeWorkspace.Global;
            using( g.Region() )
            {
                g.Append( "namespace CK.StObj" )
                 .OpenBlock()
                 .Append( _localNamespaces )
                 .Append( "static class EndpointHelper" )
                 .OpenBlock()
                    .Append( _addGlobalServiceMapping )
                    .Append( _checkAndNormalizeAmbientServices )
                    .CreatePart( out var helperExtension )
                 .CloseBlock()
                 .Append( _scopedDataHolder )
                 .Append( _uniqueServiceDescriptor );

                if( !hasEndpoint )
                {
                    g.Append( _endpointTypeInternalNoEndpoint );
                    helperExtension.Append( _fillStObjMappingsNoEndpoint );
                }
                else
                {
                    g.Append( _typedServiceDescriptor )
                     .Append( _endpointTypeInternalWithEndpoints )
                     .Append( _mapping )
                     .Append( _globalServices )
                     .Append( _diContainerType )
                     .Append( _finalConfigurationBuilder );
                    helperExtension.Append( _createInitialMapping )
                                   .Append( _fillStObjMappingsWithEndpoints );
                }
                g.CloseBlock();
            }
        }
    }
}
