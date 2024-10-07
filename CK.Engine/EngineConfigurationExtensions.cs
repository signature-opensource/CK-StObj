
using CK.Core;
using CK.Engine.TypeCollector;
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
    /// This configuration is first normalized and should have no error: if <see cref="EngineConfiguration.NormalizeConfiguration(IActivityMonitor)"/>
    /// fails, a null result is returned, otherwise a non null result is always returned (<see cref="EngineResult.Success"/> may be false).
    /// </summary>
    /// <param name="configuration">This configuration.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <returns>A <see cref="EngineResult"/> or null if this configuration is invalid.</returns>
    public static Task<EngineResult?> RunAsync( this EngineConfiguration configuration, IActivityMonitor monitor )
    {
        Throw.CheckNotNullArgument( monitor );
        if( !configuration.NormalizeConfiguration( monitor ) )
        {
            return Task.FromResult<EngineResult?>( null );
        }
        var typeGroups = BinPathTypeGroup.Run( monitor, configuration );
        var groups = typeGroups.Groups.Select( tG => new BinPathGroup( tG ) ).ToImmutableArray();
        if( typeGroups.Success )
        {
            // Temporary use of the good old StObjEngine.
            var engine = new StObjEngine( monitor, configuration, typeGroups.Groups );
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
