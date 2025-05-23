using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using CK.Core;

namespace CK.Setup;

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
    /// Initializes a new default <see cref="StObjCollector"/>.
    /// </summary>
    public StObjCollector()
        : this( new SimpleServiceContainer() )
    {
    }

    /// <summary>
    /// Initializes a new <see cref="StObjCollector"/>.
    /// </summary>
    /// <param name="serviceProvider">Service provider used for attribute constructor injection. Must not be null.</param>
    /// <param name="traceDependencySorterInput">True to trace the input of dependency graph.</param>
    /// <param name="traceDependencySorterOutput">True to trace the sorted dependency graph.</param>
    /// <param name="configurator">Used to configure items. See <see cref="IStObjStructuralConfigurator"/>.</param>
    /// <param name="valueResolver">
    /// Used to explicitly resolve or alter StObjConstruct parameters and object ambient properties.
    /// See <see cref="IStObjValueResolver"/>.
    /// </param>
    /// <param name="names">Optional list of names for the final StObjMap. When null or empty, a single empty string is the default name.</param>
    /// 
    public StObjCollector( IServiceProvider serviceProvider,
                           bool traceDependencySorterInput = false,
                           bool traceDependencySorterOutput = false,
                           IStObjStructuralConfigurator? configurator = null,
                           IStObjValueResolver? valueResolver = null,
                           IEnumerable<string>? names = null )
    {
        _errorEntries = new List<string>();
        _tempAssembly = new DynamicAssembly();
        _cc = new CKTypeCollector( serviceProvider, _tempAssembly, names );
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
    /// If the <see cref="AutoServiceKind.IsContainerConfiguredService"/> bit set, one of the lifetime bits must be set
    /// (<see cref="AutoServiceKind.IsScoped"/> xor <see cref="AutoServiceKind.IsSingleton"/>).
    /// </para>
    /// <para>
    /// Can be called multiple times as long as no contradictory registration already exists (for instance,
    /// a service cannot be both scoped and singleton).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="type">The type to register.</param>
    /// <param name="kind">The kind of service. Must not be <see cref="ConfigurableAutoServiceKind .None"/>.</param>
    /// <returns>True on success, false on error.</returns>
    public bool SetAutoServiceKind( IActivityMonitor monitor, Type type, ConfigurableAutoServiceKind kind )
    {
        using var errorTracker = monitor.OnError( _errorEntries.Add );
        if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
        if( _cc.RegisteredTypeCount > 0 )
        {
            Throw.InvalidOperationException( $"Setting external AutoService kind must be done before registering types (there is already {_cc.RegisteredTypeCount} registered types)." );
        }
        else if( _cc.KindDetector.SetAutoServiceKind( monitor, type, kind ) != null )
        {
            // Don't register assembly as VFeature for these types: we don't want external assemblies like
            // Microsoft.AspNetCore.SignalR.Core to ba a VFeature because we configured the Microsoft.AspNetCore.SignalR.IHubContext<>
            // to be a singleton.
            // But we need the assembly registration for Roslyn meta data references.
            _cc.RegisterAssembly( monitor, type, isVFeature: false );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sets <see cref="AutoServiceKind"/> combination for a type: the type is always resolved (<see cref="SimpleTypeFinder.WeakResolver"/>).
    /// Can be called multiple times as long as no contradictory registration already exists (for instance, a <see cref="IRealObject"/>
    /// cannot be a Front service).
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="typeName">The assembly qualified type name to register.</param>
    /// <param name="kind">The kind of service. Can be <see cref="AutoServiceKind.None"/> (nothing is done except the type resolution).</param>
    /// <param name="isOptional">True to warn if the type is not found instead of logging an error and returning false.</param>
    /// <returns>True on success, false on error.</returns>
    public bool SetAutoServiceKind( IActivityMonitor monitor, string typeName, ConfigurableAutoServiceKind kind, bool isOptional )
    {
        using var errorTracker = monitor.OnError( _errorEntries.Add );
        if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
        Throw.CheckNotNullOrEmptyArgument( typeName );
        var t = SimpleTypeFinder.WeakResolver( typeName, false );
        if( t != null )
        {
            return kind == ConfigurableAutoServiceKind.None || SetAutoServiceKind( monitor, t, kind );
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
    /// Registers a type that may be a CK type (<see cref="IPoco"/>, <see cref="IAutoService"/> or <see cref="IRealObject"/>).
    /// Once the first type is registered, no more call to SetAutoServiceKind methods is allowed.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
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
    /// Once the first type is registered, no more call to SetAutoServiceKind methods is allowed.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="types">Types to register.</param>
    public void RegisterTypes( IActivityMonitor monitor, IEnumerable<Type> types )
    {
        Throw.CheckNotNullArgument( types );
        using var errorTracker = monitor.OnError( _errorEntries.Add );
        if( !_wellKnownServiceKindRegistered ) AddWellKnownServices( monitor );
        SafeTypesHandler( monitor,
                          types,
                          ( m, cc, t ) => cc.RegisterType( m, t ), false );
    }

    void SafeTypesHandler( IActivityMonitor monitor,
                           IEnumerable<Type> types,
                           Action<IActivityMonitor, CKTypeCollector, Type> a,
                           bool defineExternalCall = true )
    {
        Debug.Assert( types != null );
        if( defineExternalCall && _cc.RegisteredTypeCount > 0 )
        {
            monitor.Error( $"External definition lifetime, cardinality or Front services must be done before registering types (there is already {_cc.RegisteredTypeCount} registered types)." );
        }
        else
        {
            using( monitor.OpenTrace( $"Explicitly registering IPoco interfaces, or Real Objects or Service classes." ) )
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
    /// If there are <see cref="FatalOrErrors"/>, this throws a <see cref="InvalidOperationException"/>.
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
            // Systematically registers the DIContainerHub and the AmbientServiceHub: unit tests don't have to do it.
            // (Note that the PocoDirectory is registered by the CKTypeCollector.
            _cc.RegisterClass( monitor, typeof( DIContainerHub ) );
            _cc.RegisterClass( monitor, typeof( AmbientServiceHub ) );

            DIContainerAnalysisResult? endpoints = null;
            var (typeResult, orderedItems, orderedAfterContent, buildValueCollector) = CreateTypeAndObjectResults( monitor );
            if( orderedItems != null )
            {
                // Now that Real objects and core AutoServices are settled, creates the EndpointResult.
                // This doesn't need the full auto service resolution so we have the choice to do it before
                // or after services finalization: do it before because we need to push it down to the engine
                // map with the dirty trick below (SetFinalOrderedResults).
                using( monitor.OpenInfo( "Endpoints handling." ) )
                {
                    endpoints = DIContainerAnalysisResult.Create( monitor, typeResult.RealObjects.EngineMap, typeResult.KindComputeFacade.KindDetector );
                }
                // This is far from elegant but simplifies the engine object model:
                // We set the final ordered results on the crappy mutable EngineMap (that should
                // not exist and be replaced with intermediate - functional-like - value results).
                // This is awful!
                Throw.DebugAssert( orderedAfterContent != null );
                typeResult.SetFinalOrderedResults( orderedItems, orderedAfterContent, endpoints, typeResult.KindComputeFacade.MultipleMappings );
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
                    if( endpoints != null && endpoints.HasAmbientServices )
                    {
                        if( !endpoints.BuildAmbientServiceMappingsAndCheckDefaultProvider( monitor, typeResult.RealObjects.EngineMap.Services ) )
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

    ( CKTypeCollectorResult TypeCollectorResult,
      IReadOnlyList<MutableItem>? Ordered,
      IReadOnlyList<MutableItem>? OrderedAfterContent,
      BuildValueCollector? BuildValueCollector)  CreateTypeAndObjectResults( IActivityMonitor monitor )
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
            if( error ) return (typeResult, null, null, null);

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
                if( error ) return (typeResult, null, null, null);
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
                if( error ) return (typeResult, null, null, null);
                sortResult = DependencySorter.OrderItems(
                                               monitor,
                                               engineMap.RawMappings.Select( kv => kv.Value ),
                                               null,
                                               new DependencySorterOptions()
                                               {
                                                   SkipDependencyToContainer = true,
                                                   HookInput = _traceDepencySorterInput ? i => i.Trace( monitor ) : null,
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
                    if( error ) return (typeResult, null, null, null);
                }
            }

            Debug.Assert( sortResult != null );
            // We now can setup the final ordered list of MutableItems (i.e. of IStObjResult).
            List<MutableItem> ordered = new List<MutableItem>();
            List<MutableItem> orderedAfterContent = new List<MutableItem>();
            using( monitor.OpenInfo( "Finalizing graph." ) )
            {
                using( monitor.OpenInfo( "Calling StObjConstruct." ) )
                {
                    Throw.DebugAssert( sortResult.SortedItems != null );
                    foreach( ISortedItem sorted in sortResult.SortedItems )
                    {
                        var m = (MutableItem)sorted.Item;
                        // Calls StObjConstruct on Head for Groups.
                        bool isItem = m.ItemKind == DependentItemKindSpec.Item;
                        if( isItem || sorted.IsGroupHead )
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
                            if( isItem ) orderedAfterContent.Add( m );
                        }
                        else
                        {
                            Throw.DebugAssert( sorted.IsGroup );
                            // We may call here a ConstructContent( IReadOnlyList<IStObj> packageContent ).
                            // But... is it a good thing for a package object to know its content detail?

                            orderedAfterContent.Add( m );
                        }
                    }
                }
                if( error ) return (typeResult, null, null, null);
                using( monitor.OpenInfo( "Setting PostBuild properties and injected Objects." ) )
                {
                    // Finalize construction by injecting Real Objects
                    // and PostBuild Ambient Properties on specializations.
                    foreach( MutableItem item in engineMap.FinalImplementations )
                    {
                        item.SetPostBuildProperties( monitor );
                    }
                }
                if( error ) return (typeResult, null, null, null);
            }
            Debug.Assert( !error );
            Throw.DebugAssert( ordered.Count == orderedAfterContent.Count );
            return (typeResult, ordered, orderedAfterContent, valueCollector);
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
        for( int i = concreteClasses.Count - 1; i >= 0; --i )
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
                _configurator?.Configure( monitor, m );
            }
            while( (m = m.Specialization) != null );
        }
        return nbItems;
    }
}
