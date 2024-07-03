using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    sealed class EngineTerminateContext : IStObjEngineTerminateContext
    {
        readonly IActivityMonitor _monitor;
        readonly EngineRunContext _runContext;
        readonly EngineAspectTrampoline<IStObjEngineTerminateContext> _trampoline;

        public EngineTerminateContext( IActivityMonitor monitor, EngineRunContext runContext )
        {
            _monitor = monitor;
            _runContext = runContext;
            _trampoline = new EngineAspectTrampoline<IStObjEngineTerminateContext>( this );
        }

        public ISimpleServiceContainer ServiceContainer => _runContext.ServiceContainer;

        public IStObjEngineStatus EngineStatus => _runContext.EngineStatus;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _runContext.Aspects;

        public IGeneratedBinPath UnifiedBinPath => _runContext.PrimaryBinPath;

        public IReadOnlyList<IGeneratedBinPath> AllBinPaths => _runContext.AllBinPaths;

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
