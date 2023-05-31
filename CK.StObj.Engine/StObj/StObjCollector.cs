using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using CK.Core;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace CK.Setup
{
    /// <summary>
    /// Discovers <see cref="IRealObject"/> . 
    /// Once Types are registered, the <see cref="GetResult"/> method initializes the full object graph.
    /// </summary>
    public partial class StObjCollector
    {
        readonly CKTypeCollector _cc;
        readonly IStObjStructuralConfigurator? _configurator;
        readonly IStObjValueResolver? _valueResolver;
        readonly DynamicAssembly _tempAssembly;
        readonly bool _traceDepencySorterInput;
        readonly bool _traceDepencySorterOutput;
        readonly List<string> _errorEntries;

        bool _wellKnownServiceKindRegistered;
        bool _computedResult;

        /// <summary>
        /// Initializes a new <see cref="StObjCollector"/>.
        /// </summary>
        /// <param name="serviceProvider">Service provider used for attribute constructor injection. Must not be null.</param>
        /// <param name="traceDependencySorterInput">True to trace in <paramref name="monitor"/> the input of dependency graph.</param>
        /// <param name="traceDependencySorterOutput">True to trace in <paramref name="monitor"/> the sorted dependency graph.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        /// <param name="configurator">Used to configure items. See <see cref="IStObjStructuralConfigurator"/>.</param>
        /// <param name="valueResolver">
        /// Used to explicitly resolve or alter StObjConstruct parameters and object ambient properties.
        /// See <see cref="IStObjValueResolver"/>.
        /// </param>
        /// <param name="names">Optional list of names for the final StObjMap. When null or empty, a single empty string is the default name.</param>
        public StObjCollector( IServiceProvider serviceProvider,
                               bool traceDependencySorterInput = false,
                               bool traceDependencySorterOutput = false,
                               IStObjTypeFilter? typeFilter = null,
                               IStObjStructuralConfigurator? configurator = null,
                               IStObjValueResolver? valueResolver = null,
                               IEnumerable<string>? names = null )
        {
            _errorEntries = new List<string>();
            _tempAssembly = new DynamicAssembly();
            Func<IActivityMonitor, Type, bool>? tFilter = null;
            if( typeFilter != null ) tFilter = typeFilter.TypeFilter;
            _cc = new CKTypeCollector( serviceProvider, _tempAssembly, tFilter, names );
            _configurator = configurator;
            _valueResolver = valueResolver;
            _traceDepencySorterInput = traceDependencySorterInput;
            _traceDepencySorterOutput = traceDependencySorterOutput;
        }

        /// <summary>
        /// Gets error or fatal errors that occurred during types registration.
        /// </summary>
        public IReadOnlyList<string> FatalOrErrors => _errorEntries;

        /// <summary>
        /// Gets ors sets whether the ordering of StObj that share the same rank in the dependency graph must be inverted.
        /// Defaults to false. (See <see cref="DependencySorter"/> for more information.)
        /// </summary>
        public bool RevertOrderingNames { get; set; }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>.
        /// <para>
        /// If the <see cref="AutoServiceKind.IsEndpointService"/> bit set, one of the lifetime bits mus be set
        /// (<see cref="AutoServiceKind.IsScoped"/> xor <see cref="AutoServiceKind.IsSingleton"/>) an the type
        /// is registered as an endpoint service in the <see cref="DefaultEndpointDefinition"/>.
        /// </para>
        /// <para>
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a Endpoint or Process service).
        /// </para>
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="kind">The kind of service. Must not be <see cref="AutoServiceKind.None"/>.</param>
        /// <returns>True on success, false on error.</returns>
        public bool SetAutoServiceKind( IActivityMonitor monitor, Type type, AutoServiceKind kind )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            if( _cc.RegisteredTypeCount > 0 )
            {
                Throw.InvalidOperationException( $"Setting external AutoService kind must be done before registering types (there is already {_cc.RegisteredTypeCount} registered types)." );
            }
            else if( _cc.KindDetector.SetAutoServiceKind( monitor, type, kind ) != null )
            {
                 return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to set or extend the availability of a service to an endpoint.
        /// <para>
        /// This method is called by the assembly <see cref="EndpointScopedServiceTypeAttribute"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="serviceType">The type of the service. Must be an interface or a class and not a <see cref="IRealObject"/> nor an open generic.</param>
        /// <param name="endpointDefinition">The <see cref="EndpointDefinition"/>'s type.</param>
        /// <returns>True on success, false on error (logged into <paramref name="monitor"/>).</returns>
        public bool SetEndpointScopedService( IActivityMonitor monitor, Type serviceType, Type endpointDefinition )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            return _cc.KindDetector.SetEndpointScopedService( monitor, serviceType, endpointDefinition );
        }

        /// <summary>
        /// Tries to define a service as a singleton managed by a <see cref="EndpointDefinition"/>.
        /// <para>
        /// This method is called by the assembly <see cref="EndpointSingletonServiceTypeAttribute"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="serviceType">The type of the service. Must be an interface or a class and not a <see cref="IRealObject"/> nor an open generic.</param>
        /// <param name="endpointDefinition">The <see cref="EndpointDefinition"/>'s type.</param>
        /// <returns>True on success, false on error (logged into <paramref name="monitor"/>).</returns>
        public bool SetEndpointSingletonService( IActivityMonitor monitor, Type serviceType, Type endpointDefinition )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            return _cc.KindDetector.SetEndpointSingletonService( monitor, serviceType, endpointDefinition );
        }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination for a type: the type is always resolved (<see cref="SimpleTypeFinder.WeakResolver"/>).
        /// Can be called multiple times as long as no contradictory registration already exists (for instance, a <see cref="IRealObject"/>
        /// cannot be a Front service).
        /// </summary>
        /// <param name="typeName">The assembly qualified type name to register.</param>
        /// <param name="kind">The kind of service. Can be <see cref="AutoServiceKind.None"/> (nothing is done except the type resolution).</param>
        /// <param name="isOptional">True to warn if the type is not found instead of logging an error and returning false.</param>
        /// <returns>True on success, false on error.</returns>
        public bool SetAutoServiceKind( IActivityMonitor monitor, string typeName, AutoServiceKind kind, bool isOptional )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            Throw.CheckNotNullOrEmptyArgument( typeName );
            var t = SimpleTypeFinder.WeakResolver( typeName, false );
            if( t != null )
            {
                return kind != AutoServiceKind.None
                        ? SetAutoServiceKind( monitor, t, kind )
                        : true;
            }
            if( isOptional )
            {
                monitor.Warn( $"Type name '{typeName}' not found. It is ignored (SetAutoServiceKind: {kind})." );
                return true;
            }
            monitor.Error( $"Unable to resolve expected type named '{typeName}' (SetAutoServiceKind: {kind})." );
            return false;
        }

        /// <summary>
        /// Registers types from multiple assemblies.
        /// Only classes and IPoco interfaces are considered.
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="assemblyNames">The assembly names to register.</param>
        /// <returns>The number of new discovered classes.</returns>
        public int RegisterAssemblyTypes( IActivityMonitor monitor, IReadOnlyCollection<string> assemblyNames )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            Throw.CheckNotNullArgument( assemblyNames );
            int totalRegistered = 0;
            using( monitor.OpenTrace( $"Registering {assemblyNames.Count} assemblies." ) )
            {
                foreach( var one in assemblyNames )
                {
                    using( monitor.OpenTrace( $"Registering assembly '{one}'." ) )
                    {
                        Assembly? a = null;
                        try
                        {
                            a = Assembly.Load( one );
                        }
                        catch( Exception ex )
                        {
                            monitor.Error( $"Error while loading assembly '{one}'.", ex );
                        }
                        if( a != null )
                        {
                            // Before registering types, we must handle the assembly Endpoint attributes 
                            // regardless of the "convergence" of the endpoint configuration: the external
                            // configuration must exist when a type is registered because it is registered
                            // only once.
                            foreach( var eA in a.GetCustomAttributes<EndpointScopedServiceTypeAttribute>() )
                            {
                                SetEndpointScopedService( monitor, eA.ServiceType, eA.EndpointDefinition );
                            }
                            foreach( var eA in a.GetCustomAttributes<EndpointSingletonServiceTypeAttribute>() )
                            {
                                SetEndpointSingletonService( monitor, eA.ServiceType, eA.EndpointDefinition );
                            }
                            int nbAlready = _cc.RegisteredTypeCount;
                            _cc.RegisterTypes( monitor, a.GetTypes() );
                            int delta = _cc.RegisteredTypeCount - nbAlready;
                            monitor.CloseGroup( $"{delta} types(s) registered." );
                            totalRegistered += delta;
                        }
                    }
                }
            }
            return totalRegistered;
        }

        /// <summary>
        /// Registers a type that may be a CK type (<see cref="IPoco"/>, <see cref="IAutoService"/> or <see cref="IRealObject"/>).
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="t">Type to register. Must not be null.</param>
        public void RegisterType( IActivityMonitor monitor, Type t )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            try
            {
                _cc.RegisterType( monitor, t );
            }
            catch( Exception ex )
            {
                monitor.Error( $"While registering type '{t.AssemblyQualifiedName}'.", ex );
            }
        }

        /// <summary>
        /// Explicitly registers a set of CK types (<see cref="IPoco"/>, <see cref="IAutoService"/> or <see cref="IRealObject"/>).
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="types">Types to register.</param>
        public void RegisterTypes( IActivityMonitor monitor, IReadOnlyCollection<Type> types )
        {
            Throw.CheckNotNullArgument( types );
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            DoRegisterTypes( monitor, types, types.Count );
        }

        /// <summary>
        /// Explicitly registers a set of CK types (<see cref="IPoco"/>, <see cref="IAutoService"/> or <see cref="IRealObject"/>) by their
        /// assembly qualified names.
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="typeNames">Assembly qualified names of the types to register.</param>
        public void RegisterTypes( IActivityMonitor monitor, IReadOnlyCollection<string> typeNames )
        {
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            Throw.CheckNotNullArgument( typeNames );
            DoRegisterTypes( monitor, typeNames.Select( n => SimpleTypeFinder.WeakResolver( n, true ) ).Select( t => t! ), typeNames.Count );
        }

        void DoRegisterTypes( IActivityMonitor monitor, IEnumerable<Type> types, int count )
        {
            SafeTypesHandler( monitor,
                              "Explicitly registering IPoco interfaces, or Real Objects or Service classes",
                              types,
                              count,
                              ( m, cc, t ) => cc.RegisterType( m, t ), false );
        }

        void SafeTypesHandler( IActivityMonitor monitor,
                               string registrationType,
                               IEnumerable<Type> types,
                               int count,
                               Action<IActivityMonitor,CKTypeCollector,Type> a,
                               bool defineExternalCall = true )
        {
            Debug.Assert( types != null );
            if( count == 0 ) return;
            if( defineExternalCall && _cc.RegisteredTypeCount > 0 )
            {
                monitor.Error( $"External definition lifetime, cardinality or Front services must be done before registering types (there is already {_cc.RegisteredTypeCount} registered types)." );
            }
            else
            {
                using( monitor.OpenTrace( $"{registrationType}: handling {count} type(s)." ) )
                {
                    try
                    {
                        foreach( var t in types )
                        {
                            a( monitor, _cc, t );
                        }
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( ex );
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets a function that will be called with the list of items once all of them are registered.
        /// </summary>
        public Action<IEnumerable<IDependentItem>>? DependencySorterHookInput { get; set; }

        /// <summary>
        /// Gets or sets a function that will be called when items have been successfully sorted.
        /// </summary>
        public Action<IEnumerable<ISortedItem>>? DependencySorterHookOutput { get; set; }

        /// <summary>
        /// Builds and returns a <see cref="StObjCollectorResult"/> if no error occurred during type registration.
        /// On error, <see cref="StObjCollectorResult.HasFatalError"/> is true and this result should be discarded.
        /// If <see cref="RegisteringFatalOrErrorCount"/> is not equal to 0, this throws a <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <returns>The result.</returns>
        public StObjCollectorResult GetResult( IActivityMonitor monitor )
        {
            Throw.CheckState( "Must be called once and only once.", !_computedResult );
            using var errorTracker = monitor.OnError( _errorEntries.Add );
            if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
            _computedResult = true;
            if( _errorEntries.Count != 0 ) Throw.InvalidOperationException( $"There are {_errorEntries.Count} registration errors." );
            try
            {
                // Systematically registers the EndpointTypeManager and DefaultEndpointDefinition.
                // (Note that the PocoDirectory is registered by the CKTypeCollector.
                _cc.RegisterClass( monitor, typeof( EndpointTypeManager ) );
                _cc.RegisterClass( monitor, typeof( DefaultEndpointDefinition ) );

                EndpointResult? endpoints = null;
                var (typeResult, orderedItems, buildValueCollector) = CreateTypeAndObjectResults( monitor );
                if( orderedItems != null )
                {
                    // Now that Real objects and core AutoServices are settled, creates the EndpointResult.
                    // This doesn't need the full auto service resolution so we have the choice to do it before
                    // or after services finalization: do it before because may be one day the final service
                    // resolution may need it.
                    using( monitor.OpenInfo( "Endpoints handling." ) )
                    {
                        endpoints = EndpointResult.Create( monitor, typeResult.RealObjects.EngineMap, typeResult.Endpoints );
                    }
                    // This is far from elegant but simplifies the engine object model:
                    // We set the final ordered results on the crappy mutable EngineMap (that should
                    // not exist and be replaced with intermediate - functional-like - value results).
                    // This is awful!
                    typeResult.SetFinalOrderedResults( orderedItems, endpoints, typeResult.KindComputeFacade.MultipleMappings );
                    if( !ServiceFinalHandling( monitor, typeResult ) )
                    {
                        // Setting the valueCollector to null indicates the error to the StObjCollectorResult.
                        buildValueCollector = null;
                    }
                    else
                    {
                        using( monitor.OpenInfo( "Checking remaining IEnumerable<> lifetime of IsMultiple interfaces." ) )
                        {
                            if( !typeResult.KindComputeFacade.FinalizeMultipleMappings( monitor, typeResult.RealObjects.EngineMap.ToLeaf ) )
                            {
                                buildValueCollector = null;
                            }
                        }
                    }
                }
                return new StObjCollectorResult( typeResult, _tempAssembly, endpoints, buildValueCollector );
            }
            catch( Exception ex )
            {
                monitor.Fatal( ex );
                throw;
            }
        }

        (CKTypeCollectorResult, IReadOnlyList<MutableItem>?, BuildValueCollector?) CreateTypeAndObjectResults( IActivityMonitor monitor )
        {
            bool error = false;
            using( monitor.OnError( () => error = true ) )
            {
                CKTypeCollectorResult typeResult;
                using( monitor.OpenInfo( "Initializing object graph." ) )
                {
                    using( monitor.OpenInfo( "Collecting Real Objects, Services, Endpoints and Poco." ) )
                    {
                        typeResult = _cc.GetResult( monitor );
                        typeResult.LogErrorAndWarnings( monitor );
                    }
                    if( !error && !typeResult.HasFatalError )
                    {
                        Debug.Assert( _tempAssembly.GetPocoDirectory() != null, "PocoSupportResult has been successfully computed since CKTypeCollector.GetResult() succeeded." );
                        using( monitor.OpenInfo( "Creating final objects and configuring items." ) )
                        {
                            int nbItems = ConfigureMutableItems( monitor, typeResult.RealObjects );
                            monitor.CloseGroup( $"{nbItems} items configured." );
                        }
                    }
                }
                if( error ) return (typeResult, null, null); 

                StObjObjectEngineMap engineMap = typeResult.RealObjects.EngineMap;
                IDependencySorterResult? sortResult = null;
                BuildValueCollector valueCollector = new BuildValueCollector();
                using( monitor.OpenInfo( "Topological graph ordering." ) )
                {
                    bool noCycleDetected = true;
                    using( monitor.OpenInfo( "Preparing dependent items." ) )
                    {
                        // Transfers construct parameters type as requirements for the object, binds dependent
                        // types to their respective MutableItem, resolve generalization and container
                        // inheritance, and initializes StObjProperties.
                        foreach( MutableItem item in engineMap.FinalImplementations )
                        {
                            noCycleDetected &= item.PrepareDependentItem( monitor, valueCollector );
                        }
                    }
                    if( error ) return (typeResult, null, null);
                    using( monitor.OpenInfo( "Resolving PreConstruct and PostBuild properties." ) )
                    {
                        // This is the last step before ordering the dependency graph: all mutable items have now been created and configured, they are ready to be sorted,
                        // except that we must first resolve AmbientProperties: computes TrackedAmbientProperties (and depending of the TrackAmbientPropertiesMode impact
                        // the requirements before sorting). This also gives IStObjValueResolver.ResolveExternalPropertyValue 
                        // a chance to configure unresolved properties. (Since this external resolution may provide a StObj, this may also impact the sort order).
                        // During this step, DirectProperties and RealObjects are also collected: all these properties are added to PreConstruct collectors
                        // or to PostBuild collector in order to always set a correctly constructed object to a property.
                        foreach( MutableItem item in engineMap.FinalImplementations )
                        {
                            item.ResolvePreConstructAndPostBuildProperties( monitor, valueCollector, _valueResolver );
                        }
                    }
                    if( error ) return (typeResult, null, null);
                    sortResult = DependencySorter.OrderItems(
                                                   monitor,
                                                   engineMap.RawMappings.Select( kv => kv.Value ),
                                                   null,
                                                   new DependencySorterOptions()
                                                   {
                                                       SkipDependencyToContainer = true,
                                                       HookInput = _traceDepencySorterInput ? i => i.Trace(monitor) : null,
                                                       HookOutput = _traceDepencySorterOutput ? i => i.Trace( monitor ) : null,
                                                       ReverseName = RevertOrderingNames
                                                   } );
                    Debug.Assert( sortResult.HasRequiredMissing == false,
                        "A missing requirement can not exist at this stage since we only inject existing Mutable items: missing unresolved dependencies are handled by PrepareDependentItems that logs Errors when needed." );
                    Debug.Assert( noCycleDetected || (sortResult.CycleDetected != null), "Cycle detected during item preparation => Cycle detected by the DependencySorter." );
                    if( !sortResult.IsComplete )
                    {
                        sortResult.LogError( monitor );
                        monitor.CloseGroup( "Ordering failed." );
                        if( error ) return (typeResult, null, null);
                    }
                }

                Debug.Assert( sortResult != null );
                // We now can setup the final ordered list of MutableItems (i.e. of IStObjResult).
                List<MutableItem> ordered = new List<MutableItem>();
                using( monitor.OpenInfo( "Finalizing graph." ) )
                {
                    using( monitor.OpenInfo( "Calling StObjConstruct." ) )
                    {
                        foreach( ISortedItem sorted in sortResult.SortedItems )
                        {
                            var m = (MutableItem)sorted.Item;
                            // Calls StObjConstruct on Head for Groups.
                            if( m.ItemKind == DependentItemKindSpec.Item || sorted.IsGroupHead )
                            {
                                m.SetSorterData( ordered.Count, sorted.Requires, sorted.Children, sorted.Groups );
                                using( monitor.OpenTrace( $"Constructing '{m}'." ) )
                                {
                                    try
                                    {
                                        m.CallConstruct( monitor, valueCollector, _valueResolver );
                                    }
                                    catch( Exception ex )
                                    {
                                        monitor.Error( ex );
                                    }
                                }
                                ordered.Add( m );
                            }
                            else
                            {
                                Debug.Assert( m.ItemKind != DependentItemKindSpec.Item && !sorted.IsGroupHead );
                                // We may call here a ConstructContent( IReadOnlyList<IStObj> packageContent ).
                                // But... is it a good thing for a package object to know its content detail?
                            }
                        }
                    }
                    if( error ) return (typeResult, null, null);
                    using( monitor.OpenInfo( "Setting PostBuild properties and injected Objects." ) )
                    {
                        // Finalize construction by injecting Real Objects
                        // and PostBuild Ambient Properties on specializations.
                        foreach( MutableItem item in engineMap.FinalImplementations )
                        {
                            item.SetPostBuildProperties( monitor );
                        }
                    }
                    if( error ) return (typeResult, null, null);
                }
                Debug.Assert( !error );
                return (typeResult, ordered, valueCollector);
            }
        }

        /// <summary>
        /// Creates the associated final object and applies configurations from top to bottom
        /// (see <see cref="MutableItem.ConfigureTopDown(IActivityMonitor, MutableItem)"/>).
        /// This is the very first step.
        /// </summary>
        int ConfigureMutableItems( IActivityMonitor monitor, RealObjectCollectorResult typeResult )
        {
            var concreteClasses = typeResult.ConcreteClasses;
            int nbItems = 0;
            for( int i = concreteClasses.Count-1; i >= 0; --i )
            {
                var pathTypes = (IReadOnlyList<MutableItem>)concreteClasses[i];
                Debug.Assert( pathTypes.Count > 0, "At least the final concrete class exists." );
                nbItems += pathTypes.Count;

                MutableItem specialization = pathTypes[pathTypes.Count - 1];

                object? theObject = specialization.CreateStructuredObject( monitor );
                // If we failed to create an instance, we ensure that an error is logged and
                // continue the process.
                if( theObject == null )
                {
                    monitor.Error( $"Unable to create an instance of '{pathTypes[pathTypes.Count - 1].RealObjectType.Type.FullName}'." );
                    continue;
                }
                // Finalize configuration by soliciting IStObjStructuralConfigurator.
                // It is important here to go top-down since specialized configuration 
                // should override more general ones.
                // Note that this works because we do NOT offer any access to Specialization 
                // in IStObjMutableItem. We offer an access to the Generalization (it is configured) and can help
                // target the root of a specialization path (typically to set ConstructParametersAboveRoot).
                Debug.Assert( typeof( IStObjMutableItem ).GetProperty( "Specialization" ) == null );
                MutableItem generalization = pathTypes[0];
                MutableItem? m = generalization;
                do
                {
                    m.ConfigureTopDown( monitor, generalization );
                    if( _configurator != null ) _configurator.Configure( monitor, m );
                }
                while( (m = m.Specialization) != null );
            }
            return nbItems;
        }
    }

}
