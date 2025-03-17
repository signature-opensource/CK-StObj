using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Diagnostics;
using CK.Engine.TypeCollector;

#nullable enable

namespace CK.Setup;

/// <summary>
/// Generic engine that runs a <see cref="EngineConfiguration"/>.
/// </summary>
public sealed class StObjEngine
{
    readonly IActivityMonitor _monitor;
    readonly GlobalTypeCache _typeCache;
    readonly RunningEngineConfiguration _config;
    Status? _status;
    EngineConfigureContext? _startContext;

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

    readonly IReadOnlyList<BinPathTypeGroup> _groups;

    public StObjEngine( IActivityMonitor monitor,
                        EngineConfiguration config,
                        GlobalTypeCache typeCache,
                        IReadOnlyList<BinPathTypeGroup> groups )
    {
        _monitor = monitor;
        _typeCache = typeCache;
        _groups = groups;
        _config = new RunningEngineConfiguration( config, groups );
    }

    public StObjEngineResult NewRun()
    {
        if( !_config.Initialize( _monitor, out bool canSkipRun ) )
        {
            return new StObjEngineResult( false, _config );
        }
        // If canSkipRun is true here it means that regarding the 2 core generated
        // artifacts (G0.cs and GeneratedAssembmy), there is nothing to do.
        using var _ = _monitor.OpenInfo( "Running Engine." );
        _status = new Status( _monitor );
        _startContext = new EngineConfigureContext( _monitor, _config, _status, canSkipRun );
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
                EngineRunContext runCtx = new EngineRunContext( _monitor, _startContext );
                // Creates the StObjCollectorResult for each group of compatible BinPaths
                // and instantiates a StObjEngineRunContext.GenPath that exposes the engine map and the dynamic assembly
                // for each of them through IGeneratedBinPath, ICodeGenerationContext and ICSCodeGenerationContext.
                foreach( var g in _config.Groups )
                {
                    using( _monitor.OpenInfo( g.IsUnifiedPure
                                                ? $"Analyzing types from Unified Working directory '{g.Configuration.Path}'."
                                                : $"Analyzing types from BinPaths '{g.SimilarConfigurations.Select( b => b.Path.Path ).Concatenate( "', '" )}'." ) )
                    {
                        StObjCollectorResult? r = UseLegacyStObjCollector( g, g.ConfiguredTypes );
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
                        _monitor.Error( errorPath.ToStringPath( elementSeparator: $"{Environment.NewLine}-> " ) );
                    }
                }
                // Always runs the aspects Termination.
                var termCtx = new EngineTerminateContext( _monitor, runCtx );
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

    StObjCollectorResult? UseLegacyStObjCollector( RunningBinPathGroup group, IConfiguredTypeSet configuredTypes )
    {
        Debug.Assert( _startContext != null, "Work started." );
        bool hasError = false;
        using( _monitor.OnError( () => hasError = true ) )
        {
            StObjCollectorResult result;
            var configurator = _startContext.Configurator.FirstLayer;
            StObjCollector stObjC = new StObjCollector( _startContext.ServiceContainer,
                                                        _config.Configuration.TraceDependencySorterInput,
                                                        _config.Configuration.TraceDependencySorterOutput,
                                                        configurator,
                                                        configurator,
                                                        group.SimilarConfigurations.Select( b => b.Name! ) );
            stObjC.RevertOrderingNames = _config.Configuration.RevertOrderingNames;
            using( _monitor.OpenInfo( group.IsUnifiedPure ? "Registering only IPoco and IRealObjects (Purely Unified BinPath)." : "Registering types." ) )
            {
                // First handles the explicit kind of Types.
                foreach( var c in configuredTypes.ConfiguredTypes )
                {
                    Throw.DebugAssert( c.Kind != ConfigurableAutoServiceKind.None );
                    stObjC.SetAutoServiceKind( _monitor, c.Type, c.Kind );
                }
                stObjC.RegisterTypes( _monitor, configuredTypes.AllTypes.Select( cT => cT.Type ) );
                // Finally, registers the types the Aspects want to register.
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
