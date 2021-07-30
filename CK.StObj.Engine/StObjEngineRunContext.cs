using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    class StObjEngineRunContext : IStObjEngineRunContext, IStObjEnginePostCodeRunContext
    {
        readonly IActivityMonitor _monitor;
        readonly StObjEngineConfigureContext _startContext;
        readonly List<GenBinPath> _binPaths;
        readonly StObjEngineAspectTrampoline<IStObjEngineRunContext> _trampoline;
        readonly StObjEngineAspectTrampoline<IStObjEnginePostCodeRunContext> _trampolinePostCode;
        readonly Dictionary<string, object> _unifiedRunCache;
        readonly Dictionary<object, object?> _codeGenerationGlobalMemory;

        internal class GenBinPath : IGeneratedBinPath, ICSCodeGenerationContext
        {
            readonly StObjEngineRunContext _global;

            public GenBinPath(
                StObjEngineRunContext global,
                StObjCollectorResult result,
                IReadOnlyCollection<BinPathConfiguration> binPathConfigurations,
                IGrouping<BinPathConfiguration, BinPathConfiguration> groupedPaths )
            {
                Debug.Assert( !result.HasFatalError );
                _global = global;
                Result = result;
                BinPathConfigurations = binPathConfigurations;
                GroupedPaths = groupedPaths;
                Memory = new Dictionary<object, object?>();
                ServiceContainer = new SimpleServiceContainer( _global.ServiceContainer );
                ServiceContainer.Add( result.DynamicAssembly.GetPocoSupportResult() );
            }

            public readonly StObjCollectorResult Result;

            public readonly IGrouping<BinPathConfiguration, BinPathConfiguration> GroupedPaths;

            public IStObjEngineMap EngineMap => Result.EngineMap!;

            public IReadOnlyCollection<BinPathConfiguration> BinPathConfigurations { get; }

            public ISimpleServiceContainer ServiceContainer { get; }

            public IDictionary<object, object?> Memory { get; }

            IGeneratedBinPath ICodeGenerationContext.UnifiedBinPath => _global.UnifiedBinPath;

            IReadOnlyList<IGeneratedBinPath> ICodeGenerationContext.AllBinPaths => _global.AllBinPaths;

            IDictionary<object, object?> ICodeGenerationContext.GlobalMemory => _global._codeGenerationGlobalMemory;

            ISimpleServiceContainer ICodeGenerationContext.GlobalServiceContainer => _global.ServiceContainer;

            bool ICodeGenerationContext.IsUnifiedRun => this == _global.UnifiedBinPath;

            void ICodeGenerationContext.SetUnifiedRunResult( string key, object o, bool addOrUpdate )
            {
                if( this != _global.UnifiedBinPath ) throw new InvalidOperationException( nameof( ICodeGenerationContext.IsUnifiedRun ) );
                if( addOrUpdate ) _global._unifiedRunCache[key] = o;
                else _global._unifiedRunCache.Add( key, o );
            }

            object ICodeGenerationContext.GetUnifiedRunResult( string key )
            {
                if( this == _global.UnifiedBinPath ) throw new InvalidOperationException( nameof( ICodeGenerationContext.IsUnifiedRun ) );
                return _global._unifiedRunCache[key];
            }

            IGeneratedBinPath ICodeGenerationContext.CurrentRun => this;

            IDynamicAssembly ICSCodeGenerationContext.Assembly => Result.DynamicAssembly;

            bool ICSCodeGenerationContext.SaveSource => BinPathConfigurations.Any( f => f.GenerateSourceFiles );

            CompileOption ICSCodeGenerationContext.CompileOption => BinPathConfigurations.Max( f => f.CompileOption );
        }


        public StObjEngineRunContext( IActivityMonitor monitor,
                                      StObjEngineConfigureContext startContext,
                                      IGrouping<BinPathConfiguration, BinPathConfiguration> primaryCompatibleBinPaths,
                                      StObjCollectorResult primaryResult,
                                      bool isUnifiedPure )
        {
            Debug.Assert( primaryResult.EngineMap != null );
            _monitor = monitor;
            _startContext = startContext;
            _binPaths = new List<GenBinPath>();
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineRunContext>( this );
            _trampolinePostCode = new StObjEngineAspectTrampoline<IStObjEnginePostCodeRunContext>( this );
            _unifiedRunCache = new Dictionary<string, object>();
            _codeGenerationGlobalMemory = new Dictionary<object, object?>();
            IsUnifiedPure = isUnifiedPure;
            AddResult( primaryCompatibleBinPaths, primaryResult );
        }

        internal void AddResult( IGrouping<BinPathConfiguration, BinPathConfiguration> binPaths, StObjCollectorResult secondaryResult )
        {
            _binPaths.Add( new GenBinPath( this, secondaryResult, binPaths.ToArray(), binPaths ) );
        }

        public IGeneratedBinPath UnifiedBinPath => _binPaths[0];

        public bool IsUnifiedPure { get; }

        ICodeGenerationContext IStObjEnginePostCodeRunContext.UnifiedBinPath => _binPaths[0];

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
            using( _monitor.OpenInfo( $"Running Aspects ({(postCode ? "Post" : "Before" )} Code Generation)." ) )
            {
                foreach( var a in _startContext.Aspects )
                {
                    using( _monitor.OpenInfo( $"Aspect: {a.GetType().FullName}." ) )
                    {
                        try
                        {
                            bool success = postCode ? a.RunPostCode( _monitor, this ) : a.Run( _monitor, this );
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
