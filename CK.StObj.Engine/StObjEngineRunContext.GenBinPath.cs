using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    partial class StObjEngineRunContext
    {
        internal sealed class GenBinPath : IGeneratedBinPath, ICSCodeGenerationContext
        {
            readonly StObjEngineRunContext _global;

            public GenBinPath( StObjEngineRunContext global,
                               StObjCollectorResult result,
                               RunningBinPathGroup group )
            {
                Debug.Assert( !result.HasFatalError );
                _global = global;
                Result = result;
                Group = group;
                Memory = new Dictionary<object, object?>();
                ServiceContainer = new SimpleServiceContainer( _global.ServiceContainer );
                ServiceContainer.Add( result.DynamicAssembly.GetPocoSupportResult() );
            }

            public readonly StObjCollectorResult Result;

            public RunningBinPathGroup Group { get; }

            public IStObjEngineMap EngineMap => Result.EngineMap!;

            public IReadOnlyCollection<IRunningBinPathConfiguration> BinPathConfigurations => Group.SimilarConfigurations;

            public ISimpleServiceContainer ServiceContainer { get; }

            public IDictionary<object, object?> Memory { get; }

            public bool IsUnifiedPure => Group.Configuration.IsUnifiedPure;

            IReadOnlyList<IGeneratedBinPath> ICodeGenerationContext.AllBinPaths => _global.AllBinPaths;

            IDictionary<object, object?> ICodeGenerationContext.GlobalMemory => _global._codeGenerationGlobalMemory;

            ISimpleServiceContainer ICodeGenerationContext.GlobalServiceContainer => _global.ServiceContainer;

            bool ICodeGenerationContext.IsPrimaryRun => this == _global.PrimaryBinPath;

            void ICodeGenerationContext.SetPrimaryRunResult( string key, object o, bool addOrUpdate )
            {
                Throw.CheckState( this == _global.PrimaryBinPath );
                if( addOrUpdate ) _global._unifiedRunCache[key] = o;
                else _global._unifiedRunCache.Add( key, o );
            }

            object ICodeGenerationContext.GetPrimaryRunResult( string key )
            {
                Throw.CheckState( this != _global.PrimaryBinPath );
                return _global._unifiedRunCache[key];
            }

            IGeneratedBinPath ICodeGenerationContext.CurrentRun => this;

            IDynamicAssembly ICSCodeGenerationContext.Assembly => Result.DynamicAssembly;

            bool ICSCodeGenerationContext.SaveSource => BinPathConfigurations.Any( f => f.GenerateSourceFiles );

            CompileOption ICSCodeGenerationContext.CompileOption => BinPathConfigurations.Max( f => f.CompileOption );
        }

    }
}
