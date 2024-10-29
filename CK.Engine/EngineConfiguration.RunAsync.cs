
using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Setup;

/// <summary>
/// The CKomposable engine entry point.
/// </summary>
public static class CKEngine
{
    /// <summary>
    /// Runs this <see cref="EngineConfiguration"/>.
    /// <list type="number">
    ///     <item>
    ///         This configuration is first normalized and should have no error.
    ///         <para>
    ///         If <see cref="EngineConfiguration.NormalizeConfiguration(IActivityMonitor)"/> fails, a null result is returned.
    ///         </para>
    ///     </item>
    ///     <item>
    ///         <see cref="EngineAspect{T}"/> are created and initialized. If this fails, a null result also is returned.
    ///     </item>
    /// </list>
    /// This configuration is first normalized and should have no error: if <see cref="EngineConfiguration.NormalizeConfiguration(IActivityMonitor)"/>
    /// fails, a null result is returned, otherwise a non null result is always returned (<see cref="EngineResult.Success"/> may be false).
    /// </summary>
    /// <param name="configuration">This configuration.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>A <see cref="EngineResult"/> or null if this configuration is invalid.</returns>
    public static Task<EngineResult?> RunAsync( this EngineConfiguration configuration, IActivityMonitor monitor )
    {
        Throw.CheckNotNullArgument( monitor );
        if( !configuration.NormalizeConfiguration( monitor, traceNormalizedConfiguration: false ) )
        {
            return Task.FromResult<EngineResult?>( null );
        }
        var realObjectConfigurator = new StObjEngineConfigurator();
        if( !AspectInitializer.CreateAndInitializeAspects( monitor, configuration, realObjectConfigurator, out var engineServices, out var aspects ) )
        {
            return Task.FromResult<EngineResult?>( null );
        }
        monitor.Info( $"Running Engine configuration:{Environment.NewLine}{configuration.ToXml()}" );

        var typeGroups = BinPathTypeGroup.Run( monitor, configuration );
        var groups = typeGroups.Groups.Select( tG => new BinPathGroup( tG ) ).ToImmutableArray();
        if( typeGroups.Success )
        {
            // Temporary use of the good old StObjEngine.
            var engine = new StObjEngine( monitor, configuration, typeGroups.TypeCache, typeGroups.Groups );
            var r = engine.NewRun();
            for( int i = 0; i < r.Groups.Count; i++ )
            {
                groups[i]._runningGroup = r.Groups[i];
            }
            return Task.FromResult<EngineResult?>( new EngineResult( r.Success ? RunStatus.Succeed : RunStatus.Failed, configuration, groups ) );
        }
        else
        {
            return Task.FromResult<EngineResult?>( new EngineResult( RunStatus.Failed, configuration, groups ) );
        }
    }

}
