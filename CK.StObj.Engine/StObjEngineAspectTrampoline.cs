using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup

{
    class StObjEngineAspectTrampoline<T>
    {
        readonly T _holder;
        readonly List<Func<IActivityMonitor, T, bool>> _postActions;

        public StObjEngineAspectTrampoline( T holder )
        {
            _holder = holder;
            _postActions = new List<Func<IActivityMonitor, T, bool>>();
        }

        public void Push( Func<IActivityMonitor, T, bool> deferredAction )
        {
            if( deferredAction == null ) throw new ArgumentNullException( nameof( deferredAction ) );
            _postActions.Add( deferredAction );
        }

        public bool Execute( IActivityMonitor m, Func<bool> onError )
        {
            bool trampolineSuccess = true;
            int i = 0;
            using( m.OpenInfo( $"Executing initial {_postActions.Count} deferred actions." )
                    .ConcludeWith( () => $"Executed {i} actions." ) )
            {
                while( i < _postActions.Count )
                {
                    var a = _postActions[i];
                    _postActions[i++] = null;
                    try
                    {
                        if( !a( m, _holder ) )
                        {
                            m.Error( "A deferred action failed." );
                            trampolineSuccess = onError();
                        }
                    }
                    catch( Exception ex )
                    {
                        m.Error( ex );
                        trampolineSuccess = onError();
                    }
                }
                _postActions.Clear();
            }
            return trampolineSuccess;
        }
    }
}
