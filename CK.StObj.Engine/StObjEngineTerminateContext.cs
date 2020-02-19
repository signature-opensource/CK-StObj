using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    class StObjEngineTerminateContext : IStObjEngineTerminateContext
    {
        readonly IActivityMonitor _monitor;
        readonly StObjEngineRunContext _runContext;
        readonly StObjEngineAspectTrampoline<IStObjEngineTerminateContext> _trampoline;

        public StObjEngineTerminateContext( IActivityMonitor monitor, StObjEngineRunContext runContext )
        {
            _monitor = monitor;
            _runContext = runContext;
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineTerminateContext>( this );
        }

        public ISimpleServiceContainer ServiceContainer => _runContext.ServiceContainer;

        public IStObjEngineStatus EngineStatus => _runContext.EngineStatus;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _runContext.Aspects;

        public IReadOnlyList<IStObjResult> OrderedStObjs => _runContext.OrderedStObjs;

        public void PushDeferredAction( Func<IActivityMonitor, IStObjEngineTerminateContext, bool> postAction ) => _trampoline.Push( postAction );

        internal void TerminateAspects( Func<bool> onError )
        {
            using( _monitor.OpenInfo( "Terminating Aspects." ) )
            {
                for( int i = _runContext.Aspects.Count-1; i >= 0; --i )
                {
                    IStObjEngineAspect a = _runContext.Aspects[i];
                    using( _monitor.OpenInfo( $"Aspect: {a.GetType().FullName}." ) )
                    {
                        try
                        {
                            if( !a.Terminate( _monitor, this ) ) onError();
                        }
                        catch( Exception ex )
                        {
                            _monitor.Error( ex );
                            onError();
                        }
                    }
                }
                _trampoline.Execute( _monitor, onError );
            }
        }

    }
}
