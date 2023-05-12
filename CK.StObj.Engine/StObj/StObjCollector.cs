using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using CK.Core;
using System.Reflection;

namespace CK.Setup
{

    /// <summary>
    /// Discovers available structure objects and instantiates them. 
    /// Once Types are registered, the <see cref="GetResult"/> method initializes the full object graph.
    /// </summary>
    public partial class StObjCollector
    {
        readonly CKTypeCollector _cc;
        readonly IStObjStructuralConfigurator? _configurator;
        readonly IStObjValueResolver? _valueResolver;
        readonly IActivityMonitor _monitor;
        readonly DynamicAssembly _tempAssembly;
        int _registerFatalOrErrorCount;
        bool _computedResult;

        /// <summary>
        /// Initializes a new <see cref="StObjCollector"/>.
        /// </summary>
        /// <param name="monitor">Logger to use. Can not be null.</param>
        /// <param name="serviceProvider">Service provider used for attribute constructor injection. Must not be null.</param>
        /// <param name="traceDependencySorterInput">True to trace in <paramref name="monitor"/> the input of dependency graph.</param>
        /// <param name="traceDepedencySorterOutput">True to trace in <paramref name="monitor"/> the sorted dependency graph.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        /// <param name="configurator">Used to configure items. See <see cref="IStObjStructuralConfigurator"/>.</param>
        /// <param name="valueResolver">
        /// Used to explicitly resolve or alter StObjConstruct parameters and object ambient properties.
        /// See <see cref="IStObjValueResolver"/>.
        /// </param>
        /// <param name="names">Optional list of names for the final StObjMap. When null or empty, a single empty string is the default name.</param>
        public StObjCollector( IActivityMonitor monitor,
                               IServiceProvider serviceProvider,
                               bool traceDependencySorterInput = false,
                               bool traceDepedencySorterOutput = false,
                               IStObjTypeFilter? typeFilter = null,
                               IStObjStructuralConfigurator? configurator = null,
                               IStObjValueResolver? valueResolver = null,
                               IEnumerable<string>? names = null )
        {
            Throw.CheckNotNullArgument( monitor );
            _monitor = monitor;
            _tempAssembly = new DynamicAssembly();
            Func<IActivityMonitor, Type, bool>? tFilter = null;
            if( typeFilter != null ) tFilter = typeFilter.TypeFilter;
            _cc = new CKTypeCollector( _monitor, serviceProvider, _tempAssembly, tFilter, names );
            _configurator = configurator;
            _valueResolver = valueResolver;
            if( traceDependencySorterInput ) DependencySorterHookInput = i => i.Trace( monitor );
            if( traceDepedencySorterOutput ) DependencySorterHookOutput = i => i.Trace( monitor );

            AddWellKnownServices();
        }

        /// <summary>
        /// Gets the count of error or fatal that occurred during types registration.
        /// </summary>
        public int RegisteringFatalOrErrorCount => _registerFatalOrErrorCount;

        /// <summary>
        /// Gets ors sets whether the ordering of StObj that share the same rank in the dependency graph must be inverted.
        /// Defaults to false. (See <see cref="DependencySorter"/> for more information.)
        /// </summary>
        public bool RevertOrderingNames { get; set; }

        /// <summary>
        /// Sets <see cref="AutoServiceKind"/> combination (that must not be <see cref="AutoServiceKind.None"/>) for a type.
        /// Can be called multiple times as long as no contradictory registration already exists (for instance,
        /// a <see cref="IRealObject"/> cannot be a Front service).
        /// </summary>
        /// <param name="type">The type to register.</param>
        /// <param name="kind">The kind of service. Must not be <see cref="AutoServiceKind.None"/>.</param>
        /// <returns>True on success, false on error.</returns>
        public bool SetAutoServiceKind( Type type, AutoServiceKind kind )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( kind == AutoServiceKind.None ) throw new ArgumentOutOfRangeException( nameof( kind ) );
            if( _cc.RegisteredTypeCount > 0 )
            {
                _monitor.Error( $"Setting external AutoService kind must be done before registering types (there is already {_cc.RegisteredTypeCount} registered types)." );
            }
            else if( _cc.KindDetector.SetAutoServiceKind( _monitor, type, kind ) != null )
            {
                 return true;
            }
            ++_registerFatalOrErrorCount;
            return false;
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
        public bool SetAutoServiceKind( string typeName, AutoServiceKind kind, bool isOptional )
        {
            if( String.IsNullOrWhiteSpace( typeName ) ) throw new ArgumentNullException( nameof( typeName ) );
            var t = SimpleTypeFinder.WeakResolver( typeName, false );
            if( t != null )
            {
                return kind != AutoServiceKind.None
                        ? SetAutoServiceKind( t, kind )
                        : true;
            }
            if( isOptional )
            {
                _monitor.Warn( $"Type name '{typeName}' not found. It is ignored (SetAutoServiceKind: {kind})." );
                return true;
            }
            ++_registerFatalOrErrorCount;
            _monitor.Error( $"Unable to resolve expected type named '{typeName}' (SetAutoServiceKind: {kind})." );
            return false;
        }

        /// <summary>
        /// Registers types from multiple assemblies.
        /// Only classes and IPoco interfaces are considered.
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="assemblyNames">The assembly names to register.</param>
        /// <returns>The number of new discovered classes.</returns>
        public int RegisterAssemblyTypes( IReadOnlyCollection<string> assemblyNames )
        {
            if( assemblyNames == null ) throw new ArgumentNullException( nameof( assemblyNames ) );
            int totalRegistered = 0;
            using( _monitor.OnError( () => ++_registerFatalOrErrorCount ) )
            using( _monitor.OpenTrace( $"Registering {assemblyNames.Count} assemblies." ) )
            {
                foreach( var one in assemblyNames )
                {
                    using( _monitor.OpenTrace( $"Registering assembly '{one}'." ) )
                    {
                        Assembly? a = null;
                        try
                        {
                            a = Assembly.Load( one );
                        }
                        catch( Exception ex )
                        {
                            _monitor.Error( $"Error while loading assembly '{one}'.", ex );
                        }
                        if( a != null )
                        {
                            int nbAlready = _cc.RegisteredTypeCount;
                            _cc.RegisterTypes( a.GetTypes() );
                            int delta = _cc.RegisteredTypeCount - nbAlready;
                            _monitor.CloseGroup( $"{delta} types(s) registered." );
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
        public void RegisterType( Type t )
        {
            using( _monitor.OnError( () => ++_registerFatalOrErrorCount ) )
            {
                try
                {
                    _cc.RegisterType( t );
                }
                catch( Exception ex )
                {
                    _monitor.Error( $"While registering type '{t.AssemblyQualifiedName}'.", ex );
                }
            }
        }

        /// <summary>
        /// Explicitly registers a set of CK types (<see cref="IPoco"/>, <see cref="IAutoService"/> or <see cref="IRealObject"/>).
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="types">Types to register.</param>
        public void RegisterTypes( IReadOnlyCollection<Type> types )
        {
            if( types == null ) throw new ArgumentNullException( nameof( types ) );
            DoRegisterTypes( types, types.Count );
        }

        /// <summary>
        /// Explicitly registers a set of CK types (<see cref="IPoco"/>, <see cref="IAutoService"/> or <see cref="IRealObject"/>) by their
        /// assembly qualified names.
        /// Once the first type is registered, no more call to <see cref="SetAutoServiceKind(Type, AutoServiceKind)"/> is allowed.
        /// </summary>
        /// <param name="typeNames">Assembly qualified names of the types to register.</param>
        public void RegisterTypes( IReadOnlyCollection<string> typeNames )
        {
            if( typeNames == null ) throw new ArgumentNullException( nameof( typeNames ) );
            DoRegisterTypes( typeNames.Select( n => SimpleTypeFinder.WeakResolver( n, true ) ).Select( t => t! ), typeNames.Count );
        }

        void DoRegisterTypes( IEnumerable<Type> types, int count )
        {
            SafeTypesHandler( "Explicitly registering IPoco interfaces, or Real Objects or Service classes", types, count, ( m, cc, t ) => cc.RegisterType( t ), false );
        }

        void SafeTypesHandler( string registrationType, IEnumerable<Type> types, int count, Action<IActivityMonitor,CKTypeCollector,Type> a, bool defineExternalCall = true )
        {
            Debug.Assert( types != null );
            if( count == 0 ) return;
            if( defineExternalCall && _cc.RegisteredTypeCount > 0 )
            {
                ++_registerFatalOrErrorCount;
                _monitor.Error( $"External definition lifetime, cardinality or Front services must be done before registering types (there is already {_cc.RegisteredTypeCount} registered types)." );
            }
            else
            {
                using( _monitor.OnError( () => ++_registerFatalOrErrorCount ) )
                using( _monitor.OpenTrace( $"{registrationType}: handling {count} type(s)." ) )
                {
                    try
                    {
                        foreach( var t in types )
                        {
                            a( _monitor, _cc, t );
                        }
                    }
                    catch( Exception ex )
                    {
                        _monitor.Error( ex );
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
        public StObjCollectorResult GetResult()
        {
            Throw.CheckState( $"There are {_registerFatalOrErrorCount} registration errors.", _registerFatalOrErrorCount == 0 );
            Throw.CheckState( "Must be called once and only once.", !_computedResult );
            _computedResult = true;
            try
            {
                // Systematically registers the EndpointTypeManager and DefaultEndpointType.
                // (Note that the PocoDirectory is registered by the CKTypeCollector.
                _cc.RegisterClass( typeof( EndpointTypeManager ) );
                _cc.RegisterClass( typeof( DefaultEndpointType ) );

                var (typeResult, orderedItems, buildValueCollector) = CreateTypeAndObjectResults();
                if( orderedItems != null )
                {
                    // This is far from elegant but simplifies the engine object model:
                    // We set the final ordered results on the crappy mutable EngineMap (that should
                    // not exist and be replaced with intermediate - functional-like - value results).
                    // But this would be a massive refactoring and this internal mutable state is, to be honest,
                    // quite convenient!
                    typeResult.SetFinalOrderedResults( orderedItems );
                    if( !RegisterServices( typeResult ) )
                    {
                        // Setting the valueCollector to null indicates the error to the StObjCollectorResult.
                        buildValueCollector = null;
                    }
                }
                return new StObjCollectorResult( typeResult, _tempAssembly, buildValueCollector );
            }
            catch( Exception ex )
            {
                _monitor.Fatal( ex );
                throw;
            }
        }

        (CKTypeCollectorResult, IReadOnlyList<MutableItem>?, BuildValueCollector?) CreateTypeAndObjectResults()
        {
            bool error = false;
            using( _monitor.OnError( () => error = true ) )
            {
                CKTypeCollectorResult typeResult;
                using( _monitor.OpenInfo( "Initializing object graph." ) )
                {
                    using( _monitor.OpenInfo( "Collecting Real Objects, Services, Type structure and Poco." ) )
                    {
                        typeResult = _cc.GetResult();
                        typeResult.LogErrorAndWarnings( _monitor );
                    }
                    if( error || typeResult.HasFatalError ) return (typeResult, null, null);
                    Debug.Assert( _tempAssembly.GetPocoSupportResult() != null, "PocoSupportResult has been successfully computed since CKTypeCollector.GetResult() succeeded." );
                    using( _monitor.OpenInfo( "Creating final objects and configuring items." ) )
                    {
                        int nbItems = ConfigureMutableItems( typeResult.RealObjects );
                        _monitor.CloseGroup( $"{nbItems} items configured." );
                    }
                }
                if( error ) return (typeResult, null, null); 

                StObjObjectEngineMap engineMap = typeResult.RealObjects.EngineMap;
                IDependencySorterResult? sortResult = null;
                BuildValueCollector valueCollector = new BuildValueCollector();
                using( _monitor.OpenInfo( "Topological graph ordering." ) )
                {
                    bool noCycleDetected = true;
                    using( _monitor.OpenInfo( "Preparing dependent items." ) )
                    {
                        // Transfers construct parameters type as requirements for the object, binds dependent
                        // types to their respective MutableItem, resolve generalization and container
                        // inheritance, and initializes StObjProperties.
                        foreach( MutableItem item in engineMap.FinalImplementations )
                        {
                            noCycleDetected &= item.PrepareDependentItem( _monitor, valueCollector );
                        }
                    }
                    if( error ) return (typeResult, null, null);
                    using( _monitor.OpenInfo( "Resolving PreConstruct and PostBuild properties." ) )
                    {
                        // This is the last step before ordering the dependency graph: all mutable items have now been created and configured, they are ready to be sorted,
                        // except that we must first resolve AmbientProperties: computes TrackedAmbientProperties (and depending of the TrackAmbientPropertiesMode impact
                        // the requirements before sorting). This also gives IStObjValueResolver.ResolveExternalPropertyValue 
                        // a chance to configure unresolved properties. (Since this external resolution may provide a StObj, this may also impact the sort order).
                        // During this step, DirectProperties and RealObjects are also collected: all these properties are added to PreConstruct collectors
                        // or to PostBuild collector in order to always set a correctly constructed object to a property.
                        foreach( MutableItem item in engineMap.FinalImplementations )
                        {
                            item.ResolvePreConstructAndPostBuildProperties( _monitor, valueCollector, _valueResolver );
                        }
                    }
                    if( error ) return (typeResult, null, null);
                    sortResult = DependencySorter.OrderItems(
                                                   _monitor,
                                                   engineMap.RawMappings.Select( kv => kv.Value ),
                                                   null,
                                                   new DependencySorterOptions()
                                                   {
                                                       SkipDependencyToContainer = true,
                                                       HookInput = DependencySorterHookInput,
                                                       HookOutput = DependencySorterHookOutput,
                                                       ReverseName = RevertOrderingNames
                                                   } );
                    Debug.Assert( sortResult.HasRequiredMissing == false,
                        "A missing requirement can not exist at this stage since we only inject existing Mutable items: missing unresolved dependencies are handled by PrepareDependentItems that logs Errors when needed." );
                    Debug.Assert( noCycleDetected || (sortResult.CycleDetected != null), "Cycle detected during item preparation => Cycle detected by the DependencySorter." );
                    if( !sortResult.IsComplete )
                    {
                        sortResult.LogError( _monitor );
                        _monitor.CloseGroup( "Ordering failed." );
                        if( error ) return (typeResult, null, null);
                    }
                }

                Debug.Assert( sortResult != null );
                // We now can setup the final ordered list of MutableItems (i.e. of IStObjResult).
                List<MutableItem> ordered = new List<MutableItem>();
                using( _monitor.OpenInfo( "Finalizing graph." ) )
                {
                    using( _monitor.OpenInfo( "Calling StObjConstruct." ) )
                    {
                        foreach( ISortedItem sorted in sortResult.SortedItems )
                        {
                            var m = (MutableItem)sorted.Item;
                            // Calls StObjConstruct on Head for Groups.
                            if( m.ItemKind == DependentItemKindSpec.Item || sorted.IsGroupHead )
                            {
                                m.SetSorterData( ordered.Count, sorted.Requires, sorted.Children, sorted.Groups );
                                using( _monitor.OpenTrace( $"Constructing '{m}'." ) )
                                {
                                    try
                                    {
                                        m.CallConstruct( _monitor, valueCollector, _valueResolver );
                                    }
                                    catch( Exception ex )
                                    {
                                        _monitor.Error( ex );
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
                    using( _monitor.OpenInfo( "Setting PostBuild properties and injected Objects." ) )
                    {
                        // Finalize construction by injecting Real Objects
                        // and PostBuild Ambient Properties on specializations.
                        foreach( MutableItem item in engineMap.FinalImplementations )
                        {
                            item.SetPostBuildProperties( _monitor );
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
        int ConfigureMutableItems( RealObjectCollectorResult typeResult )
        {
            var concreteClasses = typeResult.ConcreteClasses;
            int nbItems = 0;
            for( int i = concreteClasses.Count-1; i >= 0; --i )
            {
                var pathTypes = (IReadOnlyList<MutableItem>)concreteClasses[i];
                Debug.Assert( pathTypes.Count > 0, "At least the final concrete class exists." );
                nbItems += pathTypes.Count;

                MutableItem specialization = pathTypes[pathTypes.Count - 1];

                object? theObject = specialization.CreateStructuredObject( _monitor );
                // If we failed to create an instance, we ensure that an error is logged and
                // continue the process.
                if( theObject == null )
                {
                    _monitor.Error( $"Unable to create an instance of '{pathTypes[pathTypes.Count - 1].RealObjectType.Type.FullName}'." );
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
                    m.ConfigureTopDown( _monitor, generalization );
                    if( _configurator != null ) _configurator.Configure( _monitor, m );
                }
                while( (m = m.Specialization) != null );
            }
            return nbItems;
        }
    }

}
