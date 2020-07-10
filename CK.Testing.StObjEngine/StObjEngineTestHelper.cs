using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjEngine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Testing
{
    /// <summary>
    /// Standard implementation of <see cref="IStObjEngineTestHelperCore"/>.
    /// </summary>
    public partial class StObjEngineTestHelper : IStObjEngineTestHelperCore
    {
        readonly IMonitorTestHelper _monitor;

        internal StObjEngineTestHelper( IMonitorTestHelper monitor )
        {
            _monitor = monitor;
        }

        class TypeFilter : IStObjTypeFilter
        {
            readonly Func<Type, bool> _typeFilter;

            public TypeFilter( Func<Type, bool> typeFilter )
            {
                _typeFilter = typeFilter;
            }

            bool IStObjTypeFilter.TypeFilter( IActivityMonitor monitor, Type t )
            {
                return _typeFilter.Invoke( t );
            }
        }

        StObjCollector IStObjEngineTestHelperCore.CreateStObjCollector( Func<Type, bool>? typeFilter ) => DoCreateStObjCollector( typeFilter );

        StObjCollector DoCreateStObjCollector( Func<Type, bool>? typeFilter )
        {
            return new StObjCollector(
                        _monitor.Monitor,
                        new SimpleServiceContainer(),
                        typeFilter: typeFilter != null ? new TypeFilter( typeFilter ) : null );
        }

        StObjCollector IStObjEngineTestHelperCore.CreateStObjCollector( params Type[] types )
        {
            var c = DoCreateStObjCollector( null );
            c.RegisterTypes( types );
            return c;
        }

        StObjCollectorResult IStObjEngineTestHelperCore.GetSuccessfulResult( StObjCollector c ) => DoGetSuccessfulResult( c );

        static StObjCollectorResult DoGetSuccessfulResult( StObjCollector c )
        {
            c.RegisteringFatalOrErrorCount.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
            StObjCollectorResult? r = c.GetResult();
            r.HasFatalError.Should().Be( false, "There must be no error." );
            return r;
        }

        StObjCollectorResult? IStObjEngineTestHelperCore.GetFailedResult( StObjCollector c )
        {
            if( c.RegisteringFatalOrErrorCount != 0 )
            {
                TestHelper.Monitor.Error( $"GetFailedResult: {c.RegisteringFatalOrErrorCount} fatal or error during StObjCollector registration. (Everything is fine since an error was expected.)" );
                return null;
            }
            var r = c.GetResult();
            r.HasFatalError.Should().Be( true, "GetFailedResult: StObjCollector.GetResult() must have failed with at least one fatal error." );
            return r;
        }

        (StObjCollectorResult Result, IStObjMap Map) IStObjEngineTestHelperCore.CompileAndLoadStObjMap( StObjCollector c ) => DoCompileAndLoadStObjMap( c, false );

        static (StObjCollectorResult Result, IStObjMap Map) DoCompileAndLoadStObjMap( StObjCollector c, bool skipEmbeddedStObjMap )
        {
            GenerateCodeResult r = DoGenerateCode( c, CompileOption.Compile, out string assemblyName, skipEmbeddedStObjMap );
            r.CodeGen.Success.Should().BeTrue( "CodeGeneration should work." );
            var map = skipEmbeddedStObjMap ? null : r.EmbeddedStObjMap;
            if( map == null )
            {
                var a = Assembly.Load( new AssemblyName( assemblyName ) );
                map = StObjContextRoot.Load( a, TestHelper.Monitor );
                map.Should().NotBeNull();
            }
            return (r.Collector, map!);
        }

        GenerateCodeResult IStObjEngineTestHelperCore.GenerateCode( StObjCollector c, CompileOption compileOption ) => DoGenerateCode( c, compileOption, out _, false );

        GenerateCodeResult IStObjEngineTestHelperCore.GenerateCode( StObjCollectorResult r, CompileOption compileOption ) => DoGenerateCode( r, compileOption, out _, false );

        static GenerateCodeResult DoGenerateCode( StObjCollector c, CompileOption compileOption, out string assemblyName, bool skipEmbeddedStObjMap )
        {
            return DoGenerateCode( DoGetSuccessfulResult( c ), compileOption, out assemblyName, skipEmbeddedStObjMap );
        }

        /// <summary>
        /// Compiles from a successful <see cref="StObjCollectorResult"/>. <see cref="StObjCollectorResult.HasFatalError"/> must be
        /// false otherwise an <see cref="ArgumentException"/> is thrown.
        /// <para>
        /// This is a minimalist helper that simply calls <see cref="SimpleEngineRunContext.TryGenerateAssembly"/> with an
        /// assembly name that is <c>DateTime.Now.ToString( "Service_yyMdHmsffff" )</c>.
        /// </para>
        /// </summary>
        /// <param name="result">The collector result.</param>
        /// <param name="compileOption">Compilation behavior.</param>
        /// <param name="assemblyName">The automatically computed assembly name that has been generated based on current time.</param>
        /// <param name="skipEmbeddedStObjMap">
        /// True to skip any available StObjMap: this MUST be true when
        /// a setup depends on externally injected services.
        /// </param>
        /// <returns>The (successful) collector result and generation code result (that may be in error).</returns>
        static GenerateCodeResult DoGenerateCode( StObjCollectorResult result, CompileOption compileOption, out string assemblyName, bool skipEmbeddedStObjMap )
        {
            if( result.HasFatalError ) throw new ArgumentException( "StObjCollectorResult.HasFatalError must be false.", nameof( result ) );
            assemblyName = DateTime.Now.ToString( "Service_yyMdHmsffff" );
            StObjCollectorResult.CodeGenerateResult r = SimpleEngineRunContext.TryGenerateAssembly( TestHelper.Monitor, result, compileOption, skipEmbeddedStObjMap, assemblyName );
            if( skipEmbeddedStObjMap ) return new GenerateCodeResult( result, r, null );

            IStObjMap? embedded = null;
            // Always update the Project files, even if an error occurred. Consider the embedded StObjMap only
            // if the generation succeeded.
            var h = new ProjectSourceFileHandler( TestHelper.Monitor, AppContext.BaseDirectory, TestHelper.TestProjectFolder );
            if( h.MoveFilesAndCheckSignature( r ) && r.Success )
            {
                Debug.Assert( r.GeneratedSignature.HasValue );
                embedded = StObjContextRoot.Load( r.GeneratedSignature.Value, TestHelper.Monitor );
                TestHelper.Monitor.Info( embedded == null ? "No embedded generated source code." : "Embedded generated source code is available." );
            }
            return new GenerateCodeResult( result, r, embedded );
        }

        (StObjCollectorResult Result, IStObjMap Map, StObjContextRoot.ServiceRegister ServiceRegisterer, IServiceProvider Services) IStObjEngineTestHelperCore.GetAutomaticServices( StObjCollector c, SimpleServiceContainer? startupServices )
        {
            var (result, map) = DoCompileAndLoadStObjMap( c, skipEmbeddedStObjMap: startupServices != null );
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );
            return (result, map, reg, reg.Services.BuildServiceProvider());
        }

        StObjContextRoot.ServiceRegister IStObjEngineTestHelperCore.GetFailedAutomaticServicesConfiguration( StObjCollector c, SimpleServiceContainer? startupServices )
        {
            IStObjMap map = DoCompileAndLoadStObjMap( c, skipEmbeddedStObjMap: startupServices != null ).Map;
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            reg.AddStObjMap( map ).Should().BeFalse( "Service configuration failed." );
            return reg;
        }

        /// <summary>
        /// Gets the <see cref="IStObjEngineTestHelper"/> default implementation.
        /// </summary>
        public static IStObjEngineTestHelper TestHelper => TestHelperResolver.Default.Resolve<IStObjEngineTestHelper>();

    }
}
