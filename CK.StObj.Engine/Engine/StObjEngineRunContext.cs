using System;
using System.Collections.Generic;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    sealed partial class StObjEngineRunContext : IStObjEngineRunContext, IStObjEnginePostCodeRunContext
    {
        readonly IActivityMonitor _monitor;
        readonly StObjEngineConfigureContext _startContext;
        readonly List<GenBinPath> _binPaths;
        readonly StObjEngineAspectTrampoline<IStObjEngineRunContext> _trampoline;
        readonly StObjEngineAspectTrampoline<IStObjEnginePostCodeRunContext> _trampolinePostCode;
        readonly Dictionary<string, object> _primaryRunCache;

        public StObjEngineRunContext( IActivityMonitor monitor, StObjEngineConfigureContext startContext )
        {
            _monitor = monitor;
            _startContext = startContext;
            _binPaths = new List<GenBinPath>();
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineRunContext>( this );
            _trampolinePostCode = new StObjEngineAspectTrampoline<IStObjEnginePostCodeRunContext>( this );
            _primaryRunCache = new Dictionary<string, object>();
        }

        internal void AddResult( RunningBinPathGroup g, StObjCollectorResult secondaryResult )
        {
            _binPaths.Add( new GenBinPath( this, secondaryResult, g ) );
        }

        public IGeneratedBinPath PrimaryBinPath => _binPaths[0];

        IReadOnlyList<IGeneratedBinPath> IStObjEngineRunContext.AllBinPaths => _binPaths;

        IReadOnlyList<ICodeGenerationContext> IStObjEnginePostCodeRunContext.AllBinPaths => _binPaths;

        public IReadOnlyList<GenBinPath> AllBinPaths => _binPaths;

        public IStObjEngineStatus EngineStatus => _startContext.EngineStatus;

        public ISimpleServiceContainer ServiceContainer => _startContext.ServiceContainer;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _startContext.Aspects;

        public void PushDeferredAction( Func<IActivityMonitor, IStObjEngineRunContext, bool> postAction ) => _trampoline.Push( postAction );

        public void PushDeferredAction( Func<IActivityMonitor, IStObjEnginePostCodeRunContext, bool> postAction ) => _trampolinePostCode.Push( postAction );

        internal void RunAspects( Func<bool> onError, bool postCode )
        {
            using( _monitor.OpenInfo( $"Running Aspects ({(postCode ? "Post" : "Pre" )} Code Generation)." ) )
            {
                foreach( var a in _startContext.Aspects )
                {
                    using( _monitor.OpenInfo( $"Aspect: {a.GetType()}." ) )
                    {
                        try
                        {
                            bool success = postCode ? a.RunPostCode( _monitor, this ) : a.RunPreCode( _monitor, this );
                            if( !success ) onError();
                        }
                        catch( Exception ex )
                        {
                            _monitor.Error( ex );
                            onError();
                        }
                    }
                }
                if( postCode ) _trampolinePostCode.Execute( _monitor, onError );
                else _trampoline.Execute( _monitor, onError );
            }
        }

    }
}
