
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

    sealed class Mapping
    {
        object? _global;
        object? _endpoint;
        bool _isScoped;

        public Mapping( ServiceDescriptor? global, ServiceDescriptor? endpoint )
        {
            _global = global;
            _endpoint = endpoint;
            _isScoped = true;
        }

        public void AddGlobal( ServiceDescriptor d )
        {
            if( _global == null ) _global = d;
            else if( _global is List<ServiceDescriptor> l ) l.Add( d );
            else _global = new List<ServiceDescriptor>() { (ServiceDescriptor)_global, d };
        }

        public void AddEndpoint( ServiceDescriptor d )
        {
            if( _endpoint == null ) _endpoint = d;
            else if( _endpoint is List<ServiceDescriptor> l ) l.Add( d );
            else _endpoint = new List<ServiceDescriptor>() { (ServiceDescriptor)_endpoint, d };
        }

        public bool IsEmpty => _global == null && _endpoint == null;

        // We process the mapping only if there is at least 2 entries.
        // A single item uses the standard Service provider implementation.
        // Calling this clears the Endpoint: the mapping can be reused for the next
        // endpoint to configure.
        public bool ShouldProcess( out object? endpoint )
        {
            endpoint = _endpoint;
            _endpoint = null;
            return endpoint is List<ServiceDescriptor>
                                    || (_global != null && (endpoint != null || _global is List<ServiceDescriptor>));
        }

        public object? Global => _global;

        public object? Endpoint => _endpoint;

        // True by default: if the IEnumerable can be singleton, it will be.
        // When LockAsSingleton() is called this is definitely false and no Scoped
        // services can appear in the IEnumerable: this is used when a [IsMultiple] 
        // multiple has been resolved to a singleton.
        public bool IsScoped => _isScoped;

        public void LockAsSingleton() => _isScoped = false;
    }

    interface IEndpointTypeInternal : IEndpointType
    {
        bool ConfigureServices( IActivityMonitor monitor,
                                IStObjMap stObjMap,
                                IEnumerable<ServiceDescriptor> commonEndpoint,
                                Dictionary<Type, Mapping> externalMappings );
    }

    static class EndpointHelper
    {
        internal static IServiceCollection CreateCommonEndpointContainer( IActivityMonitor monitor,
                                                                            IServiceCollection global,
                                                                            Func<Type, bool> isEndpointService,
                                                                            Dictionary<Type, Mapping> externalMappings )
        {
            ServiceCollection endpoint = new ServiceCollection();
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
                if( d.Lifetime == ServiceLifetime.Singleton && d.ImplementationInstance == null )
                {
                    // If it's a singleton with a type mapping, we must add the relay to the Global only once.
                    if( !TrackMappings( externalMappings, t, d ) )
                    {
                        // Configure the relay to the last registered singleton.
                        endpoint.AddSingleton( t, sp => GetGlobalProvider( sp ).GetService( t )! );
                    }
                }
                else
                {
                    // For scope and "true" singletons (the instance is provided), this is simple:
                    // we reuse the service descriptor instance.
                    endpoint.Add( d );
                    // And we track duplicates to handle its IEnumerable<T> registration.
                    TrackMappings( externalMappings, t, d );
                }
            }
            return endpoint;

            static bool TrackMappings( Dictionary<Type, Mapping> globalMappings, Type t, ServiceDescriptor d )
            {
                if( globalMappings.TryGetValue( t, out var exists ) )
                {
                    exists.AddGlobal( d );
                    return true;
                }
                globalMappings.Add( t, new Mapping( d, null ) );
                return false;
            }
        }


        internal static IServiceProvider GetGlobalProvider( IServiceProvider sp ) => Unsafe.As<EndpointTypeManager>( sp.GetService( typeof( EndpointTypeManager ) )! ).GlobalServiceProvider;
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

        sealed class GlobalServiceExists : IServiceProviderIsService
        {
            readonly IReadOnlyDictionary<Type, Mapping> _externalMappings;

            public GlobalServiceExists( IReadOnlyDictionary<Type, Mapping> externalMappings )
            {
                _externalMappings = externalMappings;
            }

            public bool IsService( Type serviceType ) => _externalMappings.TryGetValue( serviceType, out var m ) && !m.IsEmpty;
        }

        public bool ConfigureServices( IActivityMonitor monitor,
                                        IStObjMap stObjMap,
                                        IEnumerable<ServiceDescriptor> commonEndpointContainer,
                                        Dictionary<Type, Mapping> externalMappings )
        {
            var endpoint = new ServiceCollection();
            // Calls the ConfigureEndpointServices on an empty configuration.
            _definition.ConfigureEndpointServices( endpoint, new GlobalServiceExists( externalMappings ) );

            // Process the registrations to detect:
            // - extra registrations: they must not be types mapped to IRealObject or IAutoService.
            // - missing registrations from the definition.
            // And updates the mappings with potential Mapping.Endpoint objects.
            if( CheckRegistrations( monitor, endpoint, _definition, stObjMap, externalMappings ) )
            {
                var configuration = new ServiceCollection();
                // Prepends the common endpoint configuration.
                configuration.AddRange( commonEndpointContainer );
                // Appends the endpoint configuration.
                configuration.AddRange( endpoint );
                // Generates the Multiple descriptors.
                var multipleHelper = new ExternalMultipleHelper( _definition.Name, externalMappings );
                multipleHelper.AddMultipleDescriptors( monitor, configuration );
                // Add the scoped data holder.
                var scopedDataType = typeof( EndpointScopeData<TScopeData> );
                configuration.Add( new ServiceDescriptor( scopedDataType, scopedDataType, ServiceLifetime.Scoped ) );
                // Waiting for .Net 8.
                // configuration.MakeReadOnly();
                _configuration = configuration;

                return _initializationSuccess = true;
            }
            return false;

            static bool CheckRegistrations( IActivityMonitor monitor,
                                            ServiceCollection configuration,
                                            EndpointDefinition definition,
                                            IStObjMap stObjMap,
                                            Dictionary<Type, Mapping> mappings )
            {
                var handledSingletons = new List<Type>( definition.SingletonServices );
                var handledScoped = new List<Type>( definition.ScopedServices );
                List<Type>? moreSingletons = null;
                List<Type>? moreScoped = null;
                foreach( var d in configuration )
                {
                    if( mappings.TryGetValue( d.ServiceType, out var exists ) )
                    {
                        exists.AddEndpoint( d );
                    }
                    else mappings.Add( d.ServiceType, new Mapping( null, d ) );

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
                                if( autoMap is IStObjFinalImplementation realObject  )
                                {
                                    monitor.Error( $"Endpoint '{definition.Name}' cannot configure the {lt} '{s:C}': it is mapped to the real object '{autoMap.ClassType:C}'." );
                                    success = false;
                                }
                                else
                                {
                                    monitor.Error( $"Endpoint '{definition.Name}' cannot configure the {lt} '{s:C}': it is a {(autoMap.IsScoped ? "Scoped" : "Singleton")} automatic service mapped to '{autoMap.ClassType:C}'." );
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

    // This ExternalMultipleHelper helps handling IEnumerable resolution for external mappings AND auto service mappings.
    //
    // This is used at runtime but by generated code:
    // - For each auto service mapping, if one or more external registration exist, we merge their resolution
    //   by introducing any external registrations (singleton instance, singleton mapped and scoped) into
    //   the generated code that handles automatic services.
    //
    // There is one serious error case to consider: when the auto service IEnumerable is singleton and an
    // external scoped registration exists, we are stuck.
    //
    readonly struct ExternalMultipleHelper
    {
        readonly List<Type> _singGlobal;
        readonly List<Type> _singLocal;
        readonly List<object> _singInst;
        readonly List<Type> _scopTypes;
        readonly List<string> _typeMappedErrors;
        readonly string _endpointName;
        readonly IReadOnlyDictionary<Type, Mapping> _mappings;

        public ExternalMultipleHelper( string endpointName,
                                        IReadOnlyDictionary<Type, Mapping> mappings )
        {
            _endpointName = endpointName;
            _mappings = mappings;
            _singGlobal = new List<Type>();
            _singLocal = new List<Type>();
            _singInst = new List<object>();
            _scopTypes = new List<Type>();
            _typeMappedErrors = new List<string>();
        }

        public record struct Result( bool IsPureGlobalSingleton,
                                        Type[]? SingGlobal,
                                        Type[]? SingLocal,
                                        object[]? SingInst,
                                        Type[]? ScopTypes,
                                        int Count );

        /// <summary>
        /// Each call to ProcessMultipleMappedType removes the type processed from the initial externalMappings map.
        /// The externalMappings that remains are then processed by this method to register the IEnumerable service
        /// descriptor. Type mapped to a single registration are ignored.
        /// </summary>
        public void AddMultipleDescriptors( IActivityMonitor monitor, ServiceCollection endpoint )
        {
            foreach( var (t, o) in _mappings )
            {
                if( !o.ShouldProcess( out var mappingEndpoint ) ) continue;

                var r = ProcessType( monitor, t, o.IsScoped, o.Global, mappingEndpoint );

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

        Result ProcessType( IActivityMonitor monitor, Type t, bool isScoped, object? global, object? endpoint )
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
                count += singLocal.Length;
            }
            object[]? singInst = null;
            if( _singInst.Count > 0 )
            {
                singInst = _singInst.ToArray();
                count += singInst.Length;
            }
            if( !isScoped && _singLocal.Count == 0 && _scopTypes.Count == 0 )
            {
                return new Result( true, singGlobal, null, singInst, null, _singGlobal.Count + _singInst.Count );
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
                    return new Result( false, null, null, null, null, 0 );
                }
                scopTypes = _scopTypes.ToArray();
                count += scopTypes.Length;
            }

            return new Result( false, singGlobal, singLocal, singInst, scopTypes, count );

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

                    static void HandleImplementationType( ServiceDescriptor ext,
                                                            ServiceLifetime lt,
                                                            List<Type> list,
                                                            List<string> mappedTypeError )
                    {
                        var implType = GetImplementationType( ext );
                        if( implType == ext.ServiceType || implType == typeof( object ) )
                        {
                            mappedTypeError.Add( $"Unable to analyze {lt} '{ext.ServiceType.ToCSharpName()}' type: its registration doesn't capture the target implementation type." );
                        }
                        else if( list.Contains( implType ) )
                        {
                            mappedTypeError.Add( $"Duplicate mapping from {lt} '{ext.ServiceType.ToCSharpName()}' to '{implType:C}' type." );
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
                            Type[]? typeArguments = d.ImplementationFactory!.GetType().GenericTypeArguments;
                            return typeArguments[1];
                        }

                    }
                }
            }

        }

    }

}
