using CK.Core;
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
    /// to call <see cref="StObjCollectorResult.GenerateSourceCodeFirstPass"/>
    /// and <see cref="StObjCollectorResult.GenerateSourceCodeSecondPass"/>.
    /// </summary>
    public class SimpleEngineRunContext
    {
        readonly List<CodeGenerationContext> _all;
        readonly Dictionary<string, object> _unifiedRunCache;
        readonly Dictionary<object, object?> _globalMemory;
        readonly SimpleServiceContainer _globalServiceContainer;

        /// <summary>
        /// Mutable implementation of <see cref="IGeneratedBinPath"/>.
        /// </summary>
        public class GeneratedBinPath : IGeneratedBinPath
        {
            readonly ISimpleServiceContainer _container;
            StObjCollectorResult? _result;

            /// <summary>
            /// Initializes a new <see cref="GeneratedBinPath"/>.
            /// </summary>
            /// <param name="g">The context.</param>
            public GeneratedBinPath( SimpleEngineRunContext g )
            {
                _container = new SimpleServiceContainer( g._globalServiceContainer );
                Memory = new Dictionary<object, object?>();
            }

            /// <summary>
            /// Gets or sets a <see cref="StObjCollectorResult"/>.
            /// </summary>
            public StObjCollectorResult? Result
            {
                get => _result;
                set
                {
                    if( _result != value )
                    {
                        if( _result != null ) _container.Remove<IPocoSupportResult>();
                        if( value != null )
                        {
                            var pocoSupport = value.DynamicAssembly.GetPocoSupportResult();
                            if( pocoSupport != null ) _container.Add( pocoSupport );
                        }
                        _result = value;
                    }
                }
            }

            /// <summary>
            /// Mutable list of <see cref="BinPathConfiguration"/>.
            /// There is no check about these.
            /// </summary>
            public List<BinPathConfiguration> BinPathConfigurations { get; } = new List<BinPathConfiguration>();

            ISimpleServiceContainer IGeneratedBinPath.ServiceContainer => _container;

            IStObjEngineMap IGeneratedBinPath.EngineMap => Result!.EngineMap!;

            public IDictionary<object, object?> Memory { get; }


            IReadOnlyCollection<BinPathConfiguration> IGeneratedBinPath.BinPathConfigurations => BinPathConfigurations;
        }

        /// <summary>
        /// Mutable implementation of <see cref="ICodeGenerationContext"/>.
        /// </summary>
        public class CodeGenerationContext : ICodeGenerationContext
        {
            readonly StObjEngine.SimpleEngineRunContext _global;
            readonly GeneratedBinPath _currentRun;

            internal CodeGenerationContext( StObjEngine.SimpleEngineRunContext global )
            {
                _global = global;
                _currentRun = new GeneratedBinPath( global );
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
            public GeneratedBinPath CurrentRun => _currentRun;

            IGeneratedBinPath ICodeGenerationContext.CurrentRun => CurrentRun;

            /// <summary>
            /// Gets the assembly of the <see cref="CurrentRun"/>.<see cref="GeneratedBinPath.Result"/>.
            /// </summary>
            public IDynamicAssembly Assembly => CurrentRun!.Result!.DynamicAssembly;

            bool ICodeGenerationContext.IsUnifiedRun => CurrentRun == UnifiedBinPath;

            IDictionary<object, object?> ICodeGenerationContext.GlobalMemory => _global._globalMemory;

            ISimpleServiceContainer ICodeGenerationContext.GlobalServiceContainer => _global._globalServiceContainer;

            /// <inheritdoc />
            public bool SaveSource { get; set; }

            /// <inheritdoc />
            public CompileOption CompileOption { get; set; }

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
            _globalServiceContainer = new SimpleServiceContainer();
            UnifiedBinPath = AddContext().CurrentRun;
            UnifiedBinPath.Result = unifiedResult;
        }

        /// <summary>
        /// Gets the <see cref="UnifiedBinPath"/>.
        /// </summary>
        public GeneratedBinPath UnifiedBinPath { get; }

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

        /// <summary>
        /// Attempts to generate an assembly from a single context scaffolded on a <see cref="StObjCollectorResult"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="result">The valid result.</param>
        /// <param name="compileOption">Compilation behavior.</param>
        /// <param name="skipEmbeddedStObjMap">
        /// True to skip any available StObjMap: this MUST be true when
        /// a setup depends on externally injected services.
        /// </param>
        /// <param name="assemblyName">The assembly to generate.</param>
        /// <param name="saveSource">False to not save the generated source files.</param>
        /// <returns><see cref="StObjCollectorResult.CodeGenerateResult"/>.</returns>
        public static StObjCollectorResult.CodeGenerateResult TryGenerateAssembly(
            IActivityMonitor monitor,
            StObjCollectorResult result,
            CompileOption compileOption,
            bool skipEmbeddedStObjMap,
            string assemblyName = StObjEngineConfiguration.DefaultGeneratedAssemblyName,
            bool saveSource = true )
        {
            if( result.HasFatalError ) return default;
            var ctx = new SimpleEngineRunContext( result );
            ctx.UnifiedCodeContext.CompileOption = compileOption;
            ctx.UnifiedCodeContext.SaveSource = saveSource;
            var secondPass = new List<SecondPassCodeGeneration>();
            string finalFilePath = System.IO.Path.Combine( AppContext.BaseDirectory, assemblyName + ".dll" );
            if( !result.GenerateSourceCodeFirstPass( monitor, ctx.UnifiedCodeContext, null, secondPass ) ) return default;
            Func<SHA1Value, bool> mapFinder = v => StObjContextRoot.GetMapInfo( v, monitor ) != null;
            if( skipEmbeddedStObjMap ) mapFinder = v => false;
            return result.GenerateSourceCodeSecondPass( monitor, finalFilePath, ctx.UnifiedCodeContext, secondPass, mapFinder );
        }

        /// <summary>
        /// Generates an assembly from a single context scaffolded on a <see cref="StObjCollectorResult"/>
        /// or throws an <see cref="Exception"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="result">The valid result.</param>
        /// <param name="assemblyName">The assembly to generate.</param>
        /// <param name="skipEmbeddedStObjMap">
        /// True to skip any available StObjMap: this MUST be true when
        /// a setup depends on externally injected services.
        /// </param>
        /// <param name="compileOption">Compilation behavior.</param>
        /// <param name="saveSource">False to not save the generated source files.</param>
        public static void GenerateAssembly(IActivityMonitor monitor, StObjCollectorResult result, CompileOption compileOption, bool skipEmbeddedStObjMap, string assemblyName = StObjEngineConfiguration.DefaultGeneratedAssemblyName, bool saveSource = true )
        {
            if( !TryGenerateAssembly(monitor, result, compileOption, skipEmbeddedStObjMap, assemblyName, saveSource).Success )
            {
                throw new Exception( $"Unable to generate assembly '{assemblyName}'." );
            }
        }

    }


}
