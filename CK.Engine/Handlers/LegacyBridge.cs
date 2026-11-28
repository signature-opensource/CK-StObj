using CK.Core;
using System;
using System.Linq;

namespace CK.Setup;

/// <summary>
/// The first engine handler: it runs the phases of the legacy <see cref="StObjEngine"/>. Its <see cref="StObjEngine.Initialize(out bool)"/>
/// is called by the engine before the handlers since it decides whether the run is skipped.
/// </summary>
sealed class LegacyBridge : IDisposable
{
    readonly StObjEngine _engine;
    bool _collected;

    internal LegacyBridge( IActivityMonitor monitor, EngineConfiguration configuration, GenContext gen )
    {
        _engine = new StObjEngine( monitor,
                                   configuration,
                                   gen.Apps.Select( a => a.Group ).ToArray(),
                                   gen.Apps.Select( a => a.Code ).ToArray() );
    }

    internal StObjEngine Engine => _engine;

    /// <summary>
    /// Collects the types of all the Apps and runs the aspects (the database setup).
    /// </summary>
    public void Collect( IActivityMonitor monitor, GenContext gen )
    {
        _collected = true;
        if( !_engine.Collect() || !_engine.RunAspects() )
        {
            monitor.Error( "The legacy engine failed to collect the types or to run its aspects." );
        }
    }

    /// <summary>
    /// Generates the code of the legacy engine in the App's workspace (the <c>CK.StObj.GeneratedRootContext</c> among others)
    /// before the other handlers.
    /// </summary>
    public void GenerateCode( IActivityMonitor monitor, Starting<AppGenContext> app, GenContext gen )
    {
        if( !_engine.GenerateCode( gen.Apps.IndexOf( app.Value ) ) )
        {
            monitor.Error( $"The legacy engine failed to generate the code of the {app.Value}." );
        }
    }

    /// <summary>
    /// Updates the artifacts of the similar BinPaths, runs the post code aspects and terminates the legacy engine.
    /// </summary>
    public void Terminate( IActivityMonitor monitor, Ending<GenContext> gen )
    {
        bool success = _engine.UpdateGeneratedArtifacts();
        if( success ) success = _engine.RunPostCodeAspects();
        if( !_engine.Terminate( success ) )
        {
            monitor.Error( "The legacy engine failed to terminate." );
        }
    }

    /// <summary>
    /// Updates the artifacts of the similar BinPaths (as the legacy engine always did) and terminates the legacy engine.
    /// Nothing is done if the types have not been collected.
    /// </summary>
    public void Terminate( IActivityMonitor monitor, OnError<GenContext> gen )
    {
        if( _collected )
        {
            _engine.UpdateGeneratedArtifacts();
            _engine.Terminate( false );
        }
    }

    /// <summary>
    /// Disposes the legacy engine (its disposable aspects).
    /// </summary>
    public void Dispose() => _engine.Dispose();
}
