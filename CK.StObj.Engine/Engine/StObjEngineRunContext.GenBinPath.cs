using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    sealed partial class StObjEngineRunContext
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
                ConfigurationGroup = group;
                Memory = new Dictionary<object, object?>();
                ServiceContainer = new SimpleServiceContainer( _global.ServiceContainer );
                ServiceContainer.Add( result.DynamicAssembly.GetPocoDirectory() );
            }

            public readonly StObjCollectorResult Result;

            public RunningBinPathGroup ConfigurationGroup { get; }

            public IStObjEngineMap EngineMap => Result.EngineMap!;

            public ISimpleServiceContainer ServiceContainer { get; }

            public IDictionary<object, object?> Memory { get; }

            public bool IsUnifiedPure => ConfigurationGroup.IsUnifiedPure;

            IRunningBinPathGroup IGeneratedBinPath.ConfigurationGroup => ConfigurationGroup;

            IReadOnlyList<IGeneratedBinPath> ICodeGenerationContext.AllBinPaths => _global.AllBinPaths;

            bool ICodeGenerationContext.IsPrimaryRun => this == _global.PrimaryBinPath;

            void ICodeGenerationContext.SetPrimaryRunResult( string key, object o, bool addOrUpdate )
            {
                Throw.CheckState( this == _global.PrimaryBinPath );
                if( addOrUpdate ) _global._primaryRunCache[key] = o;
                else _global._primaryRunCache.Add( key, o );
            }

            object ICodeGenerationContext.GetPrimaryRunResult( string key )
            {
                Throw.CheckState( this != _global.PrimaryBinPath );
                return _global._primaryRunCache[key];
            }

            IGeneratedBinPath ICodeGenerationContext.CurrentRun => this;

            IDynamicAssembly ICSCodeGenerationContext.Assembly => Result.DynamicAssembly;

            bool ICSCodeGenerationContext.SaveSource => ConfigurationGroup.SaveSource;

            CompileOption ICSCodeGenerationContext.CompileOption => ConfigurationGroup.CompileOption;
        }

    }
}
