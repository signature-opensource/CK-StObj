using CK.Setup;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace CK.Testing.StObjEngine
{

    /// <summary>
    /// Simplified mock of the <see cref="IStObjEngineRunContext"/> that enables
    /// to call <see cref="StObjCollectorResult.GenerateFinalAssembly(Core.IActivityMonitor, ICodeGenerationContext, string, string?)"/>.
    /// </summary>
    public class SimpleEngineRunContext
    {
        readonly List<CodeGenerationContext> _all;
        readonly Dictionary<string, object> _unifiedRunCache;
        readonly Dictionary<object, object?> _globalMemory;

        /// <summary>
        /// Mutable implementation of <see cref="IGeneratedBinPath"/>.
        /// </summary>
        public class GeneratedBinPath : IGeneratedBinPath
        {
            /// <summary>
            /// Gets or sets a <see cref="StObjCollectorResult"/>.
            /// </summary>
            public StObjCollectorResult? Result { get; set; }

            /// <summary>
            /// Mutable list of <see cref="BinPathConfiguration"/>.
            /// There is no check about these.
            /// </summary>
            public List<BinPathConfiguration> BinPathConfigurations { get; } = new List<BinPathConfiguration>();

            IStObjEngineMap IGeneratedBinPath.EngineMap => Result!.EngineMap!;

            IReadOnlyCollection<BinPathConfiguration> IGeneratedBinPath.BinPathConfigurations => BinPathConfigurations;
        }

        /// <summary>
        /// Mutable implementation of <see cref="ICodeGenerationContext"/>.
        /// </summary>
        public class CodeGenerationContext : ICodeGenerationContext
        {
            readonly StObjEngine.SimpleEngineRunContext _global;

            internal CodeGenerationContext( StObjEngine.SimpleEngineRunContext global )
            {
                _global = global;
            }

            /// <summary>
            /// Gets the unified generated bin path.
            /// </summary>
            public GeneratedBinPath UnifiedBinPath => _global.UnifiedBinPath;

            IGeneratedBinPath ICodeGenerationContext.UnifiedBinPath => UnifiedBinPath;

            IReadOnlyList<IGeneratedBinPath> ICodeGenerationContext.AllBinPaths => _global._all.Select( g => g.CurrentRun ).ToArray();

            /// <summary>
            /// Gets the mutable <see cref="GeneratedBinPath"/> for this context.
            /// </summary>
            public GeneratedBinPath CurrentRun { get; } = new GeneratedBinPath();

            IGeneratedBinPath ICodeGenerationContext.CurrentRun => CurrentRun;

            /// <summary>
            /// Gets the assembly of the <see cref="CurrentRun"/>.<see cref="GeneratedBinPath.Result"/>.
            /// </summary>
            public IDynamicAssembly Assembly => CurrentRun!.Result!.DynamicAssembly;

            bool ICodeGenerationContext.IsUnifiedRun => CurrentRun == UnifiedBinPath;

            IDictionary<object, object?> ICodeGenerationContext.GlobalMemory => _global._globalMemory;

            /// <inheritdoc />
            public bool SaveSource { get; set; }

            /// <inheritdoc />
            public bool CompileSource { get; set; }

            void ICodeGenerationContext.SetUnifiedRunResult( string key, object o, bool addOrUpdate )
            {
                if( UnifiedBinPath != _global.UnifiedBinPath ) throw new InvalidOperationException( nameof( ICodeGenerationContext.IsUnifiedRun ) );
                if( addOrUpdate ) _global._unifiedRunCache[key] = o;
                else _global._unifiedRunCache.Add( key, o );
            }

            object ICodeGenerationContext.GetUnifiedRunResult( string key )
            {
                if( UnifiedBinPath == _global.UnifiedBinPath ) throw new InvalidOperationException( nameof( ICodeGenerationContext.IsUnifiedRun ) );
                return _global._unifiedRunCache[key];
            }
        }

        /// <summary>
        /// Initializes a new mock.
        /// </summary>
        /// <param name="unifiedResult">Optional result that must be valid.</param>
        public SimpleEngineRunContext( StObjCollectorResult? unifiedResult = null )
        {
            if( unifiedResult != null && unifiedResult.HasFatalError ) throw new ArgumentException( nameof( unifiedResult ) ); 
            _all = new List<CodeGenerationContext>();
            _unifiedRunCache = new Dictionary<string, object>();
            _globalMemory = new Dictionary<object, object?>();
            UnifiedBinPath = AddContext().CurrentRun;
            UnifiedBinPath.Result = unifiedResult;
        }

        /// <summary>
        /// Gets the <see cref="UnifiedBinPath"/>.
        /// </summary>
        public GeneratedBinPath UnifiedBinPath { get; } = new GeneratedBinPath();

        /// <summary>
        /// Gets the first context of <see cref="All"/>.
        /// </summary>
        public CodeGenerationContext UnifiedCodeContext => _all[0];

        /// <summary>
        /// Gets all the <see cref="CodeGenerationContext"/>.
        /// </summary>
        public IReadOnlyList<CodeGenerationContext> All => _all;

        /// <summary>
        /// Adds a new <see cref="CodeGenerationContext"/>.
        /// </summary>
        /// <returns></returns>
        public CodeGenerationContext AddContext()
        {
            var g = new CodeGenerationContext( this );
            _all.Add( g );
            return g;
        }

    }


}
