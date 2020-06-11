using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public class StObjEngineTestHelper : IStObjEngineTestHelperCore
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

        (StObjCollectorResult Result, IStObjMap Map) IStObjEngineTestHelperCore.CompileAndLoadStObjMap( StObjCollector c ) => DoCompileAndLoadStObjMap( c );

        static (StObjCollectorResult Result, IStObjMap Map) DoCompileAndLoadStObjMap( StObjCollector c )
        {
            (StObjCollectorResult r, StObjCollectorResult.CodeGenerateResult codeGen) = DoGenerateCode( c, true, out string assemblyName );
            codeGen.Success.Should().BeTrue( "CodeGeneration should work." );
            var a = Assembly.Load( new AssemblyName( assemblyName ) );
            var map = StObjContextRoot.Load( a, null, TestHelper.Monitor );
            map.Should().NotBeNull();
            return (r, map!);
        }

        (StObjCollectorResult Result, StObjCollectorResult.CodeGenerateResult CodeGenResult) IStObjEngineTestHelperCore.GenerateCode( StObjCollector c, bool compile ) => DoGenerateCode( c, compile, out _ );

        static (StObjCollectorResult,StObjCollectorResult.CodeGenerateResult) DoGenerateCode( StObjCollector c, bool compile, out string assemblyName )
        {
            var r = DoGetSuccessfulResult( c );
            assemblyName = DateTime.Now.ToString( "Service_yyMdHmsffff" );
            var assemblyPath = Path.Combine( AppContext.BaseDirectory, assemblyName + ".dll" );
            var ctx = new SimpleEngineRunContext( r );
            ctx.UnifiedCodeContext.SaveSource = true;
            ctx.UnifiedCodeContext.CompileSource = compile;
            return (r, r.GenerateFinalAssembly( TestHelper.Monitor, ctx.UnifiedCodeContext, assemblyPath, null ));
        }

        (StObjCollectorResult Result, IStObjMap Map, StObjContextRoot.ServiceRegister ServiceRegisterer, IServiceProvider Services) IStObjEngineTestHelperCore.GetAutomaticServices( StObjCollector c, SimpleServiceContainer? startupServices )
        {
            var (result, map) = DoCompileAndLoadStObjMap( c );
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );
            return (result, map, reg, reg.Services.BuildServiceProvider());
        }

        StObjContextRoot.ServiceRegister IStObjEngineTestHelperCore.GetFailedAutomaticServicesConfiguration( StObjCollector c, SimpleServiceContainer? startupServices )
        {
            IStObjMap map = DoCompileAndLoadStObjMap( c ).Map;
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
