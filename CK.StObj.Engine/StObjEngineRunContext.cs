using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    class StObjEngineRunContext : IStObjEngineRunContext
    {
        readonly IActivityMonitor _monitor;
        readonly StObjEngineConfigureContext _startContext;
        readonly StObjEngineAspectTrampoline<IStObjEngineRunContext> _trampoline;

        public StObjEngineRunContext( IActivityMonitor monitor, StObjEngineConfigureContext startContext, IReadOnlyList<IStObjResult> stObjs, IReadOnlyCollection<VFeature> features )
        {
            _monitor = monitor;
            _startContext = startContext;
            OrderedStObjs = stObjs;
            Features = features;
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineRunContext>( this );
        }

        public IStObjEngineStatus EngineStatus => _startContext.EngineStatus;

        public ISimpleServiceContainer ServiceContainer => _startContext.ServiceContainer;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _startContext.Aspects;

        public IReadOnlyList<IStObjResult> OrderedStObjs { get; }

        public IReadOnlyCollection<VFeature> Features { get; }

        public void PushDeferredAction( Func<IActivityMonitor, IStObjEngineRunContext, bool> postAction ) => _trampoline.Push( postAction );

        internal void RunAspects( Func<bool> onError )
        {
            using( _monitor.OpenInfo( "Running Aspects." ) )
            {
                foreach( var a in _startContext.Aspects )
                {
                    using( _monitor.OpenInfo( $"Aspect: {a.GetType().FullName}." ) )
                    {
                        try
                        {
                            if( !a.Run( _monitor, this ) ) onError();
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
