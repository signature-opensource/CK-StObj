using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Diagnostics;
using System.Xml.Linq;
using System.IO;
using Microsoft.Extensions.Configuration;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Generic engine that runs a <see cref="StObjEngineConfiguration"/>.
    /// </summary>
    public sealed class StObjEngine
    {
        readonly IActivityMonitor _monitor;
        readonly RunningStObjEngineConfiguration _config;
        readonly XElement? _ckSetupConfig;

        Status? _status;
        StObjEngineConfigureContext? _startContext;
        bool _hasRun;

        sealed class Status : IStObjEngineStatus, IDisposable
        {
            readonly IActivityMonitor _m;
            readonly ActivityMonitorPathCatcher _pathCatcher;
            public bool Success;

            public Status( IActivityMonitor m )
            {
                _m = m;
                _pathCatcher = new ActivityMonitorPathCatcher() { IsLocked = true };
                _m.Output.RegisterClient( _pathCatcher );
                Success = true;
            }

            bool IStObjEngineStatus.Success => Success;

            public IReadOnlyList<ActivityMonitorPathCatcher.PathElement> DynamicPath => _pathCatcher.DynamicPath;

            public IReadOnlyList<ActivityMonitorPathCatcher.PathElement>? LastErrorPath => _pathCatcher.LastErrorPath;

            public IReadOnlyList<ActivityMonitorPathCatcher.PathElement>? LastWarnOrErrorPath => _pathCatcher.LastErrorPath;

            public void Dispose()
            {
                _pathCatcher.IsLocked = false;
                _m.Output.UnregisterClient( _pathCatcher );
            }
        }

        /// <summary>
        /// Initializes a new <see cref="StObjEngine"/>.
        /// </summary>
        /// <param name="monitor">Logger that must be used.</param>
        /// <param name="config">Configuration that describes the key aspects of the build.</param>
        public StObjEngine( IActivityMonitor monitor, StObjEngineConfiguration config )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( config );
            _monitor = monitor;
            _config = new RunningStObjEngineConfiguration( config );
        }

        /// <summary>
        /// Initializes a new <see cref="StObjEngine"/> from a xml element (see <see cref="StObjEngineConfiguration(XElement)"/>).
        /// </summary>
        /// <param name="monitor">Logger that must be used.</param>
        /// <param name="config">Configuration that describes the key aspects of the build.</param>
        public StObjEngine( IActivityMonitor monitor, XElement config )
            : this( monitor, new StObjEngineConfiguration( config ) )
        {
            // We are coming from CKSetup: the configuration element has a Engine attribute.
            if( config.Attribute( "Engine" ) != null ) _ckSetupConfig = config;
        }

        /// <summary>
        /// Gets whether this engine is running or has <see cref="Run()"/> (it can run only once).
        /// </summary>
        public bool Started => _startContext != null;

        [Obsolete]
        sealed class MonoResolver : IStObjCollectorResultResolver
        {
            readonly StObjCollectorResult _result;

            public MonoResolver( StObjCollectorResult result )
            {
                _result = result;
            }

            public StObjCollectorResult? GetResult( RunningBinPathGroup g ) => _result;
        }

        /// <summary>
        /// Helper with a single <see cref="StObjCollectorResult"/> for a configuration.
        /// If the <paramref name="config"/> has more than one <see cref="StObjEngineConfiguration.BinPaths"/>,
        /// they will share the same <see cref="IRunningBinPathGroup"/>: their <see cref="BinPathConfiguration.Path"/> must be the same
        /// otherwise an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="result">The collector result.</param>
        /// <param name="config">The configuration.</param>
        /// <returns>True on success, false otherwise.</returns>
        [Obsolete( "Use the ISet<Type> or RunSingleBinPath( stObjCollectorResult ) instead." )]
        public static StObjEngineResult Run( IActivityMonitor monitor, StObjCollectorResult result, StObjEngineConfiguration config )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( result );
            Throw.CheckNotNullArgument( config );
            Throw.CheckArgument( config.BinPaths.Select( b => b.Path ).Distinct().Count() == 1 );
            var e = new StObjEngine( monitor, config );
            return e.Run( new MonoResolver( result ) );
        }


        /// <summary>
        /// Runs the setup, fully defined by the configuration.
        /// This is the entry point when run by CKSetup.
        /// </summary>
        /// <returns>True on success, false if an error occurred.</returns>
        public bool Run() => DoRun( null, null, null ).Success;

        /// <summary>
        /// Runs the setup, delegating the obtention of the <see cref="StObjCollectorResult"/> to an external resolver.
        /// </summary>
        /// <param name="resolver">The resolver to use.</param>
        /// <returns>The run result.</returns>
        [Obsolete( "Use Run( types ) or RunSingleBinPath( stObjCollectorResult ) instead." )]
        public StObjEngineResult Run( IStObjCollectorResultResolver resolver )
        {
            Throw.CheckNotNullArgument( resolver );
            return DoRun( null, null, resolver );
        }

        /// <summary>
        /// Runs the setup with explicit registered types.
        /// </summary>
        /// <param name="types">Explicit types to register.</param>
        /// <returns>The run result.</returns>
        public StObjEngineResult Run( ISet<Type> types )
        {
            Throw.CheckNotNullArgument( types );
            return DoRun( types, null, null );
        }

        /// <summary>
        /// Runs the setup with explicit registered types.
        /// </summary>
        /// <param name="stObjCollectorResult">Already available result.</param>
        /// <returns>The run result.</returns>
        public StObjEngineResult RunSingleBinPath( StObjCollectorResult stObjCollectorResult )
        {
            Throw.CheckState( _config.Configuration.BinPaths.Count == 1 );
            Throw.CheckNotNullArgument( stObjCollectorResult );
            return DoRun( null, stObjCollectorResult, null );
        }

        StObjEngineResult DoRun( ISet<Type>? types, StObjCollectorResult? stObjCollectorResult, IStObjCollectorResultResolver? obsoleteResolver )
        {
            Throw.CheckState( "Run can be called only once.", !_hasRun );
            _hasRun = true;
            if( !RunningStObjEngineConfiguration.CheckAndValidate( _monitor, _config.Configuration ) )
            {
                return new StObjEngineResult( false, _config );
            }
            if( _ckSetupConfig != null )
            {
                _config.ApplyCKSetupConfiguration( _monitor, _ckSetupConfig );
            }
            if( !_config.Initialize( _monitor, out bool canSkipRun ) )
            {
                return new StObjEngineResult( false, _config );
            }
            // If canSkipRun is true here it means that regarding the 2 core generated artifacts, there is
            // nothing to do.
            using var _ = _monitor.OpenInfo( "Running StObjEngine setup." );
            _status = new Status( _monitor );
            _startContext = new StObjEngineConfigureContext( _monitor, _config, _status, canSkipRun );
            try
            {
                // Creating and configuring the aspects.
                _startContext.CreateAndConfigureAspects( () => _status.Success = false );
                if( _status.Success && _startContext.CanSkipRun )
                {
                    _monitor.Info( "Skipping run." );
                    _status.Success |= UpdateGeneratedArtifacts( _config.Groups );
                    _startContext.OnSkippedRun( () => _status.Success = false );
                    return new StObjEngineResult( _status.Success, _config );
                }
                if( _status.Success )
                {
                    StObjEngineRunContext runCtx = new StObjEngineRunContext( _monitor, _startContext );
                    // Creates the StObjCollectorResult for each group of compatible BinPaths
                    // and instantiates a StObjEngineRunContext.GenPath that exposes the engine map and the dynamic assembly
                    // for each of them through IGeneratedBinPath, ICodeGenerationContext and ICSCodeGenerationContext.
                    foreach( var g in _config.Groups )
                    {
                        using( _monitor.OpenInfo( g.IsUnifiedPure
                                                    ? $"Analyzing types from Unified Working directory '{g.Configuration.Path}'."
                                                    : $"Analyzing types from BinPaths '{g.SimilarConfigurations.Select( b => b.Path.Path ).Concatenate( "', '" )}'." ) )
                        {
                            StObjCollectorResult? r = stObjCollectorResult ?? obsoleteResolver?.GetResult( g ) ?? SafeBuildStObj( g, types );
                            if( r == null )
                            {
                                _status.Success = false;
                                break;
                            }
                            runCtx.AddResult( g, r );
                        }
                    }
                    // This is where all aspects runs before Code generation: this is where CK.Setupable.Engine.SetupableAspect.Run():
                    // - Builds the ISetupItem items (that support 3 steps setup) associated to each StObj (relies on EngineMap.StObjs.OrderedStObjs).
                    // - Projects the StObj topological order on the ISetupItem items graph.
                    // - Calls the DynamicItemInitialize methods that can create new Setup items (typically as child of existing containers like SqlProcedure on SqlTable)
                    // - The ISetupItems are then sorted topologically (this is the second graph).
                    // - The Init/Install/Settle steps are executed.
                    if( _status.Success )
                    {
                        runCtx.RunAspects( () => _status.Success = false, postCode: false );
                    }
                    // Code Generation.
                    if( _status.Success )
                    {
                        using( _monitor.OpenInfo( "Code Generation." ) )
                        {
                            foreach( var g in runCtx.AllBinPaths )
                            {
                                if( !g.Result.GenerateSourceCode( _monitor,
                                                                  g,
                                                                  _config.Configuration.InformationalVersion,
                                                                  runCtx.Aspects.OfType<ICSCodeGenerator>() ) )
                                {
                                    _status.Success = false;
                                    break;
                                }
                            }
                        }
                    }
                    // Handling generated artifacts.
                    _status.Success &= UpdateGeneratedArtifacts( runCtx.AllBinPaths.Select( g => g.ConfigurationGroup ) );
                    // Run the aspects Post Code Generation.
                    if( _status.Success )
                    {
                        runCtx.RunAspects( () => _status.Success = false, postCode: true );
                    }
                    // Secure errors (ensure error log and logs error path).
                    if( !_status.Success )
                    {
                        // Emit the last error log path as an error and ensure that at least one error
                        // has been logged on failure.
                        var errorPath = _status.LastErrorPath;
                        if( errorPath == null || errorPath.Count == 0 )
                        {
                            _monitor.Fatal( "Success status is false but no error has been logged." );
                        }
                        else
                        {
                            _monitor.Error( errorPath.ToStringPath( elementSeparator: $"{Environment.NewLine}-> ") );
                        }
                    }
                    // Always runs the aspects Termination.
                    var termCtx = new StObjEngineTerminateContext( _monitor, runCtx );
                    termCtx.TerminateAspects( () => _status.Success = false );
                }
                return new StObjEngineResult( _status.Success, _config );
            }
            finally
            {
                DisposeDisposableAspects();
                _status.Dispose();
            }
        }

        sealed class TypeFilterFromConfiguration : IStObjTypeFilter
        {
            readonly StObjConfigurationLayer? _firstLayer;
            readonly HashSet<string> _excludedTypes;
            readonly bool _isUnifiedPure;

            public TypeFilterFromConfiguration( RunningBinPathGroup g, StObjConfigurationLayer? firstLayer )
            {
                _excludedTypes = g.Configuration.ExcludedTypes;
                _firstLayer = firstLayer;
                _isUnifiedPure = g.IsUnifiedPure;
            }

            bool IStObjTypeFilter.TypeFilter( IActivityMonitor monitor, Type t )
            {
                // Type.FullName is null if the current instance represents a generic type parameter, an array
                // type, pointer type, or byref type based on a type parameter, or a generic type
                // that is not a generic type definition but contains unresolved type parameters.
                // This FullName is also null for (at least) classes nested into nested generic classes.
                // In all cases, we emit a warn and filters this beast out.
                if( t.FullName == null )
                {
                    // Warn only if it's not a generic type definition.
                    if( !t.IsGenericTypeDefinition )
                    {
                        monitor.Warn( $"Type has no FullName: '{t:C}'. It is excluded." );
                    }
                    return false;
                }
                Throw.DebugAssert( "Since FullName is defined.", t.AssemblyQualifiedName != null );
                if( _excludedTypes.Contains( t.Name ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its Type Name." );
                    return false;
                }
                if( _excludedTypes.Contains( t.FullName ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its Type FullName." );
                    return false;
                }
                if( _excludedTypes.Contains( t.AssemblyQualifiedName ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its Type AssemblyQualifiedName." );
                    return false;
                }
                if( SimpleTypeFinder.WeakenAssemblyQualifiedName( t.AssemblyQualifiedName, out var weaken )
                    && _excludedTypes.Contains( weaken ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its weak type name ({weaken})." );
                    return false;
                }
                // We only care about IPoco and IRealObject. Nothing more.
                if( _isUnifiedPure )
                {
                    if( !typeof( IPoco ).IsAssignableFrom( t )
                        && !typeof( IRealObject ).IsAssignableFrom( t ) )
                    {
                        return false;
                    }
                }
                return _firstLayer?.TypeFilter( monitor, t ) ?? true;
            }
        }

        StObjCollectorResult? SafeBuildStObj( RunningBinPathGroup group, ISet<Type>? types )
        {
            Debug.Assert( _startContext != null, "Work started." );
            bool hasError = false;
            using( _monitor.OnError( () => hasError = true ) )
            {
                StObjCollectorResult result;
                var configurator = _startContext.Configurator.FirstLayer;
                // When head.IsUnifiedPure the type filter keeps only the IPoco and IRealObject.
                var typeFilter = new TypeFilterFromConfiguration( group, configurator );
                StObjCollector stObjC = new StObjCollector( _startContext.ServiceContainer,
                                                            _config.Configuration.TraceDependencySorterInput,
                                                            _config.Configuration.TraceDependencySorterOutput,
                                                            typeFilter,
                                                            configurator,
                                                            configurator,
                                                            group.SimilarConfigurations.Select( b => b.Name! ) );
                stObjC.RevertOrderingNames = _config.Configuration.RevertOrderingNames;
                using( _monitor.OpenInfo( group.IsUnifiedPure ? "Registering only IPoco and IRealObjects (Purely Unified BinPath).": "Registering types." ) )
                {
                    // First handles the explicit kind of Types.
                    // These are services: we don't care.
                    if( !group.IsUnifiedPure )
                    {
                        foreach( var c in group.Configuration.Types )
                        {
                            // When c.Kind is None, !Optional is challenged.
                            // The Type is always resolved.
                            stObjC.SetAutoServiceKind( _monitor, c.Name, c.Kind, c.Optional );
                        }
                    }
                    // Registers the types provided by code.
                    if( types != null ) stObjC.RegisterTypes( _monitor, types );
                    // Then registers the types from the assemblies.
                    stObjC.RegisterAssemblyTypes( _monitor, group.Configuration.Assemblies );
                    // Explicitly registers the non optional Types.
                    if( !group.IsUnifiedPure ) stObjC.RegisterTypes( _monitor, group.Configuration.Types.Where( c => c.Optional == false ).Select( c => c.Name ).ToList() );
                    // Finally, registers the code based explicitly registered types.
                    foreach( var t in _startContext.ExplicitRegisteredTypes ) stObjC.RegisterType( _monitor, t );
                }
                if( stObjC.FatalOrErrors.Count == 0 )
                {
                    using( _monitor.OpenInfo( "Resolving Real Objects & AutoService dependency graph." ) )
                    {
                        result = stObjC.GetResult( _monitor );
                        Debug.Assert( !result.HasFatalError || hasError, "result.HasFatalError ==> An error has been logged." );
                    }
                    if( !result.HasFatalError ) return result;
                }
            }
            return null;
        }

        bool UpdateGeneratedArtifacts( IEnumerable<RunningBinPathGroup> groups )
        {
            using( _monitor.OpenInfo( "Updating the generated artifacts to similar BinPaths if any." ) )
            {
                foreach( var g in groups )
                {
                    if( !g.IsUnifiedPure )
                    {
                        if( !g.UpdateSimilarArtifactsFromHead( _monitor ) ) return false;
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Disposes all disposable aspects.
        /// </summary>
        void DisposeDisposableAspects()
        {
            Debug.Assert( _startContext != null, "Work started." );
            foreach( var aspect in _startContext.Aspects.OfType<IDisposable>() )
            {
                try
                {
                    aspect.Dispose();
                }
                catch( Exception ex )
                {
                    _monitor.Error( $"While disposing Aspect '{aspect.GetType().AssemblyQualifiedName}'.", ex );
                }
            }
        }

    }
}
