using System;
using System.Collections.Generic;
using System.Linq;
using CK.CodeGen;
using CK.Core;
using CK.TypeCache;
using CK.TypeSet;

#nullable enable

namespace CK.Setup;

/// <summary>
/// The legacy engine. CK.Engine owns the run and calls its phases, in this order:
/// <list type="number">
/// <item><see cref="Initialize(out bool)"/>.</item>
/// <item>When the run can be skipped, <see cref="RunSkipped()"/> and nothing else.</item>
/// <item><see cref="Collect()"/> and <see cref="RunAspects()"/>.</item>
/// <item>For each group, <see cref="GenerateCode(int)"/>: CK.Engine then completes the code of the group, signs, saves
/// and compiles it (see <see cref="SetRunSignature(int, SHA1Value)"/>).</item>
/// <item><see cref="UpdateGeneratedArtifacts()"/> (even on failure), <see cref="RunPostCodeAspects()"/> on success.</item>
/// <item><see cref="Terminate(bool)"/>, always once <see cref="Collect()"/> has been called.</item>
/// </list>
/// This engine must be disposed.
/// </summary>
public sealed class StObjEngine : IDisposable
{
    readonly IActivityMonitor _monitor;
    readonly RunningEngineConfiguration _config;
    readonly IReadOnlyList<ICodeWorkspace> _codeWorkspaces;
    Status? _status;
    EngineConfigureContext? _startContext;
    EngineRunContext? _runContext;

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
    /// Initializes a new engine.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="groups">The groups of the BinPaths.</param>
    /// <param name="codeWorkspaces">
    /// The code workspace of each group (the <c>G0.cs</c> source code), in the order of the <paramref name="groups"/>:
    /// this engine generates its code in them.
    /// </param>
    public StObjEngine( IActivityMonitor monitor,
                        EngineConfiguration config,
                        IReadOnlyList<BinPathTypeGroup> groups,
                        IReadOnlyList<ICodeWorkspace> codeWorkspaces )
    {
        Throw.CheckArgument( codeWorkspaces != null && codeWorkspaces.Count == groups.Count );
        _monitor = monitor;
        _codeWorkspaces = codeWorkspaces;
        _config = new RunningEngineConfiguration( config, groups );
    }

    /// <summary>
    /// Gets the running groups, in the order of the groups: the first one is the unified one.
    /// </summary>
    public IReadOnlyList<IRunningBinPathGroup> Groups => _config.Groups;

    /// <summary>
    /// Initializes the groups, creates and configures the aspects.
    /// </summary>
    /// <param name="canSkipRun">
    /// True when the generated artifacts (the <c>G0.cs</c> and the generated assembly) are up to date and no aspect
    /// requires a run: <see cref="RunSkipped()"/> must be called.
    /// </param>
    /// <returns>True on success, false on error.</returns>
    public bool Initialize( out bool canSkipRun )
    {
        Throw.CheckState( _status == null );
        if( !_config.Initialize( _monitor, out canSkipRun ) )
        {
            return false;
        }
        // If canSkipRun is true here it means that regarding the 2 core generated
        // artifacts (G0.cs and GeneratedAssembly), there is nothing to do.
        _status = new Status( _monitor );
        _startContext = new EngineConfigureContext( _monitor, _config, _status, canSkipRun );
        _startContext.CreateAndConfigureAspects( () => _status.Success = false );
        canSkipRun = _startContext.CanSkipRun;
        return _status.Success;
    }

    /// <summary>
    /// Handles a skipped run: updates the generated artifacts of the similar BinPaths and calls the aspects.
    /// </summary>
    /// <returns>True on success, false on error.</returns>
    public bool RunSkipped()
    {
        Throw.CheckState( _status != null && _startContext != null && _startContext.CanSkipRun );
        _monitor.Info( "Skipping run." );
        _status.Success &= UpdateGeneratedArtifacts( _config.Groups );
        _startContext.OnSkippedRun( () => _status.Success = false );
        return _status.Success;
    }

    /// <summary>
    /// Creates the <see cref="StObjCollectorResult"/> of each group (the unified one first).
    /// </summary>
    /// <returns>True on success, false on error.</returns>
    public bool Collect()
    {
        Throw.CheckState( _status != null && _startContext != null && _runContext == null );
        _runContext = new EngineRunContext( _monitor, _startContext );
        // Creates the StObjCollectorResult for each group of compatible BinPaths
        // and instantiates a StObjEngineRunContext.GenPath that exposes the engine map and the dynamic assembly
        // for each of them through IGeneratedBinPath, ICodeGenerationContext and ICSCodeGenerationContext.
        for( int i = 0; i < _config.Groups.Count; ++i )
        {
            var g = _config.Groups[i];
            using( _monitor.OpenInfo( g.IsUnifiedPure
                                        ? $"Analyzing types from Unified Working directory '{g.Configuration.Path}'."
                                        : $"Analyzing types from BinPaths '{g.SimilarConfigurations.Select( b => b.Path.Path ).Concatenate( "', '" )}'." ) )
            {
                StObjCollectorResult? r = UseLegacyStObjCollector( g, g.ConfiguredTypes, _codeWorkspaces[i] );
                if( r == null )
                {
                    _status.Success = false;
                    break;
                }
                _runContext.AddResult( g, r );
            }
        }
        return _status.Success;
    }

    /// <summary>
    /// Runs the aspects before the code generation.
    /// This is where CK.Setupable.Engine.SetupableAspect.Run():
    /// <list type="bullet">
    /// <item>Builds the ISetupItem items (that support 3 steps setup) associated to each StObj (relies on EngineMap.StObjs.OrderedStObjs).</item>
    /// <item>Projects the StObj topological order on the ISetupItem items graph.</item>
    /// <item>Calls the DynamicItemInitialize methods that can create new Setup items (typically as child of existing containers like SqlProcedure on SqlTable).</item>
    /// <item>The ISetupItems are then sorted topologically (this is the second graph).</item>
    /// <item>The Init/Install/Settle steps are executed.</item>
    /// </list>
    /// </summary>
    /// <returns>True on success, false on error.</returns>
    public bool RunAspects()
    {
        Throw.CheckState( _status != null && _runContext != null );
        _runContext.RunAspects( () => _status.Success = false, postCode: false );
        return _status.Success;
    }

    /// <summary>
    /// Generates the source code of a group in its code workspace. CK.Engine then completes it, signs, saves and
    /// compiles it (see <see cref="SetRunSignature(int, SHA1Value)"/>).
    /// </summary>
    /// <param name="groupIndex">The index of the group in the <see cref="Groups"/>.</param>
    /// <returns>True on success, false on error.</returns>
    public bool GenerateCode( int groupIndex )
    {
        Throw.CheckState( _status != null && _runContext != null );
        var g = _runContext.AllBinPaths[groupIndex];
        if( !g.Result.GenerateSourceCode( _monitor,
                                          g,
                                          _config.Configuration.InformationalVersion,
                                          _runContext.Aspects.OfType<ICSCodeGenerator>() ) )
        {
            _status.Success = false;
        }
        return _status.Success;
    }

    /// <summary>
    /// Sets the signature of the source code of a group, computed by CK.Engine.
    /// </summary>
    /// <param name="groupIndex">The index of the group in the <see cref="Groups"/>.</param>
    /// <param name="runSignature">The signature of the source code.</param>
    public void SetRunSignature( int groupIndex, SHA1Value runSignature )
    {
        var g = _config.Groups[groupIndex];
        Throw.CheckArgument( g.RunSignature.IsZero || runSignature == g.RunSignature );
        g.RunSignature = runSignature;
    }

    /// <summary>
    /// Updates the generated artifacts of the similar BinPaths, if any.
    /// </summary>
    /// <returns>True on success, false on error.</returns>
    public bool UpdateGeneratedArtifacts()
    {
        Throw.CheckState( _status != null && _runContext != null );
        _status.Success &= UpdateGeneratedArtifacts( _runContext.AllBinPaths.Select( g => g.ConfigurationGroup ) );
        return _status.Success;
    }

    /// <summary>
    /// Runs the aspects after the code generation.
    /// </summary>
    /// <returns>True on success, false on error.</returns>
    public bool RunPostCodeAspects()
    {
        Throw.CheckState( _status != null && _runContext != null );
        _runContext.RunAspects( () => _status.Success = false, postCode: true );
        return _status.Success;
    }

    /// <summary>
    /// Ensures that an error is logged on failure and always calls the aspects' termination.
    /// </summary>
    /// <param name="success">False when a step of CK.Engine failed.</param>
    /// <returns>True on success, false on error.</returns>
    public bool Terminate( bool success )
    {
        Throw.CheckState( _status != null && _runContext != null );
        _status.Success &= success;
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
        var termCtx = new EngineTerminateContext( _monitor, _runContext );
        termCtx.TerminateAspects( () => _status.Success = false );
        return _status.Success;
    }

    /// <summary>
    /// Disposes the disposable aspects.
    /// </summary>
    public void Dispose()
    {
        if( _startContext != null )
        {
            DisposeDisposableAspects();
        }
        _status?.Dispose();
    }

    StObjCollectorResult? UseLegacyStObjCollector( RunningBinPathGroup group, IConfiguredTypeSet configuredTypes, ICodeWorkspace code )
    {
        Throw.DebugAssert( "Work started.", _startContext != null );
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
                                                        group.SimilarConfigurations.Select( b => b.Name! ),
                                                        code );
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
                    Throw.DebugAssert( "result.HasFatalError ==> An error has been logged.", !result.HasFatalError || hasError );
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
        Throw.DebugAssert( "Work started.", _startContext != null );
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
