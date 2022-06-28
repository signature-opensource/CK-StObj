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

            ISimpleServiceContainer IGeneratedBinPath.ServiceContainer => _container;

            IStObjEngineMap IGeneratedBinPath.EngineMap => Result!.EngineMap!;

            /// <inheritdoc />
            public IDictionary<object, object?> Memory { get; }

            IReadOnlyCollection<IRunningBinPathConfiguration> IGeneratedBinPath.BinPathConfigurations => Array.Empty<IRunningBinPathConfiguration>();

            bool IGeneratedBinPath.IsUnifiedPure => false;
        }

        /// <summary>
        /// Mutable implementation of <see cref="ICSCodeGenerationContext"/>.
        /// </summary>
        public class CodeGenerationContext : ICSCodeGenerationContext
        {
            readonly SimpleEngineRunContext _global;
            readonly GeneratedBinPath _currentRun;

            internal CodeGenerationContext( SimpleEngineRunContext global )
            {
                _global = global;
                _currentRun = new GeneratedBinPath( global );
            }

            /// <summary>
            /// Gets the primary bin path.
            /// </summary>
            public GeneratedBinPath PrimaryBinPath => _global.PrimaryBinPath;


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

            bool ICodeGenerationContext.IsPrimaryRun => CurrentRun == PrimaryBinPath;

            IDictionary<object, object?> ICodeGenerationContext.GlobalMemory => _global._globalMemory;

            ISimpleServiceContainer ICodeGenerationContext.GlobalServiceContainer => _global._globalServiceContainer;

            /// <inheritdoc />
            public bool SaveSource { get; set; }

            /// <inheritdoc />
            public CompileOption CompileOption { get; set; }

            void ICodeGenerationContext.SetPrimaryRunResult( string key, object o, bool addOrUpdate )
            {
                if( PrimaryBinPath != _global.PrimaryBinPath ) throw new InvalidOperationException( nameof( ICSCodeGenerationContext.IsPrimaryRun ) );
                if( addOrUpdate ) _global._unifiedRunCache[key] = o;
                else _global._unifiedRunCache.Add( key, o );
            }

            object ICodeGenerationContext.GetPrimaryRunResult( string key )
            {
                if( PrimaryBinPath == _global.PrimaryBinPath ) throw new InvalidOperationException( nameof( ICSCodeGenerationContext.IsPrimaryRun ) );
                return _global._unifiedRunCache[key];
            }
        }

        /// <summary>
        /// Initializes a new mock.
        /// </summary>
        /// <param name="unifiedResult">Optional result that must be valid.</param>
        public SimpleEngineRunContext( StObjCollectorResult? unifiedResult = null )
        {
            Throw.CheckArgument( unifiedResult == null || !unifiedResult.HasFatalError ); 
            _all = new List<CodeGenerationContext>();
            _unifiedRunCache = new Dictionary<string, object>();
            _globalMemory = new Dictionary<object, object?>();
            _globalServiceContainer = new SimpleServiceContainer();
            PrimaryBinPath = AddContext().CurrentRun;
            PrimaryBinPath.Result = unifiedResult;
        }

        /// <summary>
        /// Gets the <see cref="UnifiedBinPath"/>.
        /// </summary>
        public GeneratedBinPath PrimaryBinPath { get; }

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
        public static StObjCollectorResult.CodeGenerateResult TryGenerateAssembly( IActivityMonitor monitor,
                                                                                   StObjCollectorResult result,
                                                                                   CompileOption compileOption,
                                                                                   bool skipEmbeddedStObjMap,
                                                                                   string assemblyName = StObjContextRoot.GeneratedAssemblyName,
                                                                                   bool saveSource = true )
        {
            if( result.HasFatalError ) return default;
            var ctx = new SimpleEngineRunContext( result );
            ctx.UnifiedCodeContext.CompileOption = compileOption;
            ctx.UnifiedCodeContext.SaveSource = saveSource;
            var secondPass = new List<MultiPassCodeGeneration>();
            string finalFilePath = System.IO.Path.Combine( AppContext.BaseDirectory, assemblyName + ".dll" );
            if( !result.GenerateSourceCodeFirstPass( monitor, ctx.UnifiedCodeContext, null, secondPass ) ) return default;
            Func<IActivityMonitor, SHA1Value, bool> mapFinder = skipEmbeddedStObjMap
                            ? ( m, v ) => false
                            : ( m, v ) => StObjContextRoot.GetMapInfo( v, m ) != null;
            return result.GenerateSourceCodeSecondPass( monitor, finalFilePath, ctx.UnifiedCodeContext, SHA1Value.Zero, secondPass, mapFinder );
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
        public static void GenerateAssembly(IActivityMonitor monitor, StObjCollectorResult result, CompileOption compileOption, bool skipEmbeddedStObjMap, string assemblyName = StObjContextRoot.GeneratedAssemblyName, bool saveSource = true )
        {
            if( !TryGenerateAssembly(monitor, result, compileOption, skipEmbeddedStObjMap, assemblyName, saveSource).Success )
            {
                Throw.Exception( $"Unable to generate assembly '{assemblyName}'." );
            }
        }

    }


}
