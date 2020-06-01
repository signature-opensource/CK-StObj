using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    class StObjEngineRunContext : IStObjEngineRunContext
    {
        internal class GenBinPath : IGeneratedBinPath
        {
            public GenBinPath( StObjCollectorResult result, IReadOnlyCollection<BinPathConfiguration> binPathConfigurations, IGrouping<BinPathConfiguration, BinPathConfiguration> groupedPaths )
            {
                Debug.Assert( !result.HasFatalError );
                Result = result;
                BinPathConfigurations = binPathConfigurations;
                GroupedPaths = groupedPaths;
            }

            public readonly StObjCollectorResult Result;

            public readonly IGrouping<BinPathConfiguration, BinPathConfiguration> GroupedPaths;

            public IStObjEngineMap EngineMap => Result.EngineMap!;

            public IReadOnlyCollection<BinPathConfiguration> BinPathConfigurations { get; }
        }

        readonly IActivityMonitor _monitor;
        readonly StObjEngineConfigureContext _startContext;
        readonly List<GenBinPath> _binPaths;
        readonly StObjEngineAspectTrampoline<IStObjEngineRunContext> _trampoline;

        public StObjEngineRunContext( IActivityMonitor monitor, StObjEngineConfigureContext startContext, IGrouping<BinPathConfiguration, BinPathConfiguration> primaryCompatibleBinPaths, StObjCollectorResult primaryResult )
        {
            Debug.Assert( primaryResult.EngineMap != null );
            _monitor = monitor;
            _startContext = startContext;
            _binPaths = new List<GenBinPath>();
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineRunContext>( this );
            AddResult( primaryCompatibleBinPaths, primaryResult );
        }

        internal void AddResult( IGrouping<BinPathConfiguration, BinPathConfiguration> binPaths, StObjCollectorResult secondaryResult )
        {
            _binPaths.Add( new GenBinPath(secondaryResult, binPaths.ToArray(), binPaths) );
        }

        public IGeneratedBinPath UnifiedBinPath => _binPaths[0];

        IReadOnlyList<IGeneratedBinPath> IStObjEngineRunContext.AllBinPaths => _binPaths;

        public IReadOnlyList<GenBinPath> AllBinPaths => _binPaths;

        public IStObjEngineStatus EngineStatus => _startContext.EngineStatus;

        public ISimpleServiceContainer ServiceContainer => _startContext.ServiceContainer;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _startContext.Aspects;

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
