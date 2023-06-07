
// This code is embedded in the generated code but in the CK.StObj namespace
// in its own namespace block so that the scoped "using" are only for this.
//
// It is the core of the endpoint container implementation.
// If anything here is changed, it has to be manually reported in the code generation
// (and vice versa).
//
// File: CK.StObj.Engine\StObj\EndpointSourceCodeGenerator.cs
namespace CK.StObj.Engine.Tests
{
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
    using CK.StObj.Engine.Tests.Endpoint.Conformant;

    // This one is always required because EndpointTypeManager_CK holds an array of
    // IEndpointTypeInternal (even if it is empty).
    interface IEndpointTypeInternal : IEndpointType
    {
        bool ConfigureServices( IActivityMonitor monitor,
                                IStObjMap stObjMap,
                                Dictionary<Type,Mapping> mappings,
                                ServiceDescriptor[] trueSingletons );
    }

    sealed class Mapping
    {
        object? _global;
        ServiceDescriptor? _lastGlobal;
        object? _endpoint;
        ServiceDescriptor? _lastEndpoint;
        bool _isScoped;

        public Mapping( ServiceDescriptor? global, ServiceDescriptor? endpoint )
        {
            _global = _lastGlobal = global;
            _endpoint = _lastEndpoint = endpoint;
            _isScoped = true;
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
            return endpoint is List<ServiceDescriptor>
                                    || (_global != null && (endpoint != null || _global is List<ServiceDescriptor>));
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

    // Always injected.
    static class EndpointHelper
    {
        internal static IServiceProvider GetGlobalProvider( IServiceProvider sp ) => Unsafe.As<EndpointTypeManager>( sp.GetService( typeof( EndpointTypeManager ) )! ).GlobalServiceProvider;

        // Injected only if there are endpoints.
        internal static Dictionary<Type, Mapping> CreateInitialMapping( IActivityMonitor monitor,
                                                                        IServiceCollection global,
                                                                        Func<Type, bool> isEndpointService )
        {
            Dictionary<Type, Mapping> mappings = new Dictionary<Type, Mapping>();
            foreach( var d in global )
            {
                var t = d.ServiceType;
                if( t == typeof( EndpointTypeManager ) ) Throw.ArgumentException( "EndpointTypeManager must not be configured." );
                // Skip any endpoint service and IHostedService.
                // There's no need to have the IHostedService multiple service in the endpoint containers.
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
                    mappings.Add( t, new Mapping( d, null ) );
                }
            }
            return mappings;
        }

        internal static void FillStObjMappingsNoEndpoint( IActivityMonitor monitor,
                                                          IStObjMap stObjMap,
                                                          IServiceCollection global )
        {
            // We have no real issues for real objects: we simply create singleton descriptors
            // with the true singleton instance and add them to the global container and
            // to the mappings (if there are endpoints).
            foreach( var o in stObjMap.StObjs.FinalImplementations )
            {
                var typeMapping = new ServiceDescriptor( o.ClassType, o.Implementation );
                global.Add( typeMapping );
                foreach( var unique in o.UniqueMappings )
                {
                    var uMapping = new ServiceDescriptor( unique, o.Implementation );
                    global.Add( uMapping );
                }
                foreach( var multi in o.MultipleMappings )
                {
                    var mMapping = new ServiceDescriptor( multi, o.Implementation );
                    global.Add( mMapping );
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
                if( s.IsScoped )
                {
                    AddServiceMapping( global, s, ServiceLifetime.Scoped );
                }
                else
                {
                    if( s.ClassType == typeof( EndpointTypeManager ) ) continue;
                    AddServiceMapping( global, s, ServiceLifetime.Singleton );
                }
            }

            static void AddServiceMapping( IServiceCollection global, IStObjServiceClassDescriptor s, ServiceLifetime lt )
            {
                var typeMapping = new ServiceDescriptor( s.ClassType, s.FinalType, lt );
                global.Add( typeMapping );
                // Same delegate used for all the mappings (if any). 
                Func<IServiceProvider, object>? shared = null;
                foreach( var unique in s.UniqueMappings )
                {
                    var uMapping = new ServiceDescriptor( unique, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
                    global.Add( uMapping );
                }
                foreach( var multi in s.MultipleMappings )
                {
                    var mMapping = new ServiceDescriptor( multi, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
                    global.Add( mMapping );
                }
            }
        }

        internal static void FillStObjMappingsWithEndpoints( IActivityMonitor monitor,
                                                             IStObjMap stObjMap,
                                                             IServiceCollection global,
                                                             Dictionary<Type, Mapping> mappings )
        {
            // We have no real issues for real objects: we simply create singleton descriptors
            // with the true singleton instance and add them to the global container and
            // to the endpoint mappings.
            foreach( var o in stObjMap.StObjs.FinalImplementations )
            {
                var typeMapping = new ServiceDescriptor( o.ClassType, o.Implementation );
                global.Add( typeMapping );
                Mapping? m = new Mapping( typeMapping, null );
                // Use Add: no external configuration must register a IRealObject.
                mappings.Add( o.ClassType, m );
                foreach( var unique in o.UniqueMappings )
                {
                    var uMapping = new ServiceDescriptor( unique, o.Implementation );
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
                        mappings.Add( multi, new Mapping( mMapping, null ) );
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
                if( s.IsScoped )
                {
                    AddServiceMapping( global, mappings, s, ServiceLifetime.Scoped );
                }
                else
                {
                    if( s.ClassType == typeof( EndpointTypeManager ) ) continue;
                    AddServiceMapping( global, mappings, s, ServiceLifetime.Singleton );
                }
            }

            if( mappings != null )
            {
                // Locking the IsMultiple optimized to be singleton: this prevents
                // any multiple registration of the type with a scope lifetime.
                // If it happens (either by global configuration or by a endpoint configuration),
                // the StObjMap registration fails.
                foreach( var multiple in stObjMap.MultipleMappings.Values )
                {
                    if( !multiple.IsScoped ) mappings[multiple.ItemType].LockAsSingleton();
                }
            }

            static void AddServiceMapping( IServiceCollection global, Dictionary<Type, Mapping> mappings, IStObjServiceClassDescriptor s, ServiceLifetime lt )
            {
                var typeMapping = new ServiceDescriptor( s.ClassType, s.FinalType, lt );
                global.Add( typeMapping );
                Mapping m = new Mapping( typeMapping, null );
                mappings.Add( s.ClassType, m );
                // Same delegate used for all the mappings (if any). 
                Func<IServiceProvider, object>? shared = null;
                foreach( var unique in s.UniqueMappings )
                {
                    var uMapping = new ServiceDescriptor( unique, shared ??= (sp => sp.GetService( s.ClassType )!), lt );
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
                        mappings.Add( multi, new Mapping( mMapping, null ) );
                    }
                }
            }
        }
    }

    sealed class EndpointType<TScopedData> : IEndpointType<TScopedData>, IEndpointTypeInternal where TScopedData : EndpointDefinition.ScopedData
    {
        internal IEndpointServiceProvider<TScopedData>? _services;

        readonly EndpointDefinition<TScopedData> _definition;
        internal ServiceCollection? _configuration;
        Type[] _singletons;
        Type[] _scoped;
        readonly object _lock;
        bool _initializationSuccess;

        public EndpointType( EndpointDefinition<TScopedData> definition )
        {
            _singletons = _scoped = Type.EmptyTypes;
            _definition = definition;
            _lock = new object();
        }

        public EndpointDefinition EndpointDefinition => _definition;

        public Type ScopeDataType => typeof( TScopedData );

        public string Name => _definition.Name;

        public IEndpointServiceProvider<TScopedData> GetContainer() => _services ?? DoCreateContainer();

        public bool IsService( Type serviceType ) => GetContainer().IsService( serviceType );

        public IReadOnlyCollection<Type> SpecificSingletonServices => _singletons;

        public IReadOnlyCollection<Type> SpecificScopedServices => _scoped;

        IEndpointServiceProvider<TScopedData> DoCreateContainer()
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

        sealed class GlobalServiceExists : IServiceProviderIsService
        {
            readonly IReadOnlyDictionary<Type, Mapping> _externalMappings;

            public GlobalServiceExists( IReadOnlyDictionary<Type, Mapping> externalMappings )
            {
                _externalMappings = externalMappings;
            }

            public bool IsService( Type serviceType ) => _externalMappings.TryGetValue( serviceType, out var m ) && !m.IsEmpty;
        }

        sealed class ScopedDataHolder
        {
            [AllowNull]
            internal TScopedData _data;
        }

        sealed class Provider : IEndpointServiceProvider<TScopedData>
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
                scope.ServiceProvider.GetRequiredService<ScopedDataHolder>()._data = scopedData;
                return scope;
            }

            public IServiceScope CreateScope( TScopedData scopedData )
            {
                var scope = _serviceProvider.CreateScope();
                scope.ServiceProvider.GetRequiredService<ScopedDataHolder>()._data = scopedData;
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

        static TScopedData GetScopedData( IServiceProvider sp ) => Unsafe.As<ScopedDataHolder>( sp.GetService( typeof( ScopedDataHolder ) )! )._data;

        internal static object ResolveFromUbiquitous( Type t, IServiceProvider sp )
        {
            var data = GetScopedData( sp );
            var map = data.UbiquitousInfo.GetMapping( t );
            if( map is ITuple spMap )
            {
                return ((Func<IServiceProvider, object>)spMap[0]!).Invoke( sp );
            }
            if( map is Delegate )
            {
                // Use a regular cast: we don't control user mismatch of ScopedData. If it's wrong, we want a
                // proper cast exception (not a hard engine crash).
                return ((Func<TScopedData, object>)map).Invoke( data );
            }
            return map;
        }

        public bool ConfigureServices( IActivityMonitor monitor,
                                       IStObjMap stObjMap,
                                       Dictionary<Type, Mapping> mappings,
                                       ServiceDescriptor[] trueSingletons )
        {
            var endpoint = new ServiceCollection();
            // Calls the ConfigureEndpointServices on an empty configuration.
            _definition.ConfigureEndpointServices( endpoint, GetScopedData, new GlobalServiceExists( mappings ) );

            // Process the endpoint specific registrations to detect extra registrations: there must not be any type
            // mapped to IRealObject or IAutoService or ubiquitous scoped endpoint services.
            // This also updates the mappings with potential Mapping.Endpoint objects.
            if( CheckRegistrations( monitor, endpoint, stObjMap, mappings ) )
            {
                var configuration = new ServiceCollection();
                // Generates the Multiple descriptors.
                var builder = new FinalConfigurationBuilder( _definition.Name, mappings );
                builder.FinalConfigure( monitor, configuration );
                // Add the scoped data holder.
                var scopedDataType = typeof( ScopedDataHolder );
                configuration.Add( new ServiceDescriptor( scopedDataType, scopedDataType, ServiceLifetime.Scoped ) );
                // Add the StObjMap, the EndpointTypeManager, all the IEndpointType<TScopeData> and the
                // IEnumerable<IEndpoint>.
                configuration.AddRange( trueSingletons );

                // We must also register the ubiquitous services. Since the code depends on the
                // EndpointDefinition (because of the possibility to shortcut the EndpointUbiquitousInfo) we
                // 
                FakeEndpointTypeManager_CK.
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
                foreach( var d in configuration )
                {
                    if( mappings.TryGetValue( d.ServiceType, out var exists ) )
                    {
                        exists.AddEndpoint( d );
                    }
                    else mappings.Add( d.ServiceType, new Mapping( null, d ) );

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
                bool success = true;
                if( !ErrorNotEndpointAutoServices( monitor, _definition, stObjMap, singletons, ServiceLifetime.Singleton ) ) success = false;
                if( !ErrorNotEndpointAutoServices( monitor, _definition, stObjMap, scoped, ServiceLifetime.Scoped ) ) success = false;
                if( scoped != null ) _scoped = scoped.ToArray();
                if( singletons != null ) _singletons = singletons.ToArray();
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
                                monitor.Warn( $"Endpoint '{definition.Name}' supports the {lt} service '{s:C}' that is not declared as a endpoint service." );
                            }
                        }
                    }
                    return success;
                }
            }
        }
    }

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

        public void FinalConfigure( IActivityMonitor monitor, ServiceCollection endpoint )
        {
            foreach( var (t, m) in _mappings )
            {
                var last = m.LastEndpoint ?? m.LastGlobal;
                if( last == null ) continue;
                if( m.LastEndpoint == null
                    && last.Lifetime == ServiceLifetime.Singleton
                    && last.ImplementationInstance == null )
                {
                    endpoint.Add( new ServiceDescriptor( t, sp => EndpointHelper.GetGlobalProvider( sp ).GetService( t )!, ServiceLifetime.Singleton ) );
                }
                else
                {
                    endpoint.Add( last );
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
                                    var g = EndpointHelper.GetGlobalProvider( sp );
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
                        endpoint.Add( new ServiceDescriptor( tEnum, sp => EndpointHelper.GetGlobalProvider( sp ).GetService( tEnum )!, ServiceLifetime.Singleton ) );
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
                                        var g = EndpointHelper.GetGlobalProvider( sp );
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

}
