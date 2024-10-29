using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup;

struct AspectTrampolineImpl
{
    List<Func<IActivityMonitor, bool>> _postActions;

    public void Push( Func<IActivityMonitor, bool> deferredAction )
    {
        Throw.CheckNotNullArgument( deferredAction );
        _postActions ??= new List<Func<IActivityMonitor, bool>>();
        _postActions.Add( deferredAction );
    }

    public bool Execute( IActivityMonitor monitor )
    {
        if( _postActions == null ) return true;
        bool success = true;
        int i = 0;
        using( monitor.OpenInfo( $"Executing {_postActions.Count} deferred actions." )
                .ConcludeWith( () => $"Executed {i} actions." ) )
        {
            while( i < _postActions.Count )
            {
                var a = _postActions[i];
                try
                {
                    if( !a( monitor ) )
                    {
                        monitor.Error( "A deferred action failed." );
                        success = false;
                    }
                }
                catch( Exception ex )
                {
                    monitor.Error( ex );
                    success = false;
                }
            }
            _postActions.Clear();
        }
        return success;
    }
}
