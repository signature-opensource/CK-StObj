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
            return new StObjCollector( _monitor.Monitor,
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

        (StObjCollectorResult Result, IStObjMap Map) IStObjEngineTestHelperCore.CompileAndLoadStObjMap( StObjCollector c, Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator ) => DoCompileAndLoadStObjMap( c, engineConfigurator, false );

        static (StObjCollectorResult Result, IStObjMap Map) DoCompileAndLoadStObjMap( StObjCollector c,
                                                                                      Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                                      bool skipEmbeddedStObjMap )
        {
            GenerateCodeResult r = DoGenerateCode( TestHelper.GetSuccessfulResult( c ), engineConfigurator, skipEmbeddedStObjMap );
            r.Success.Should().BeTrue( "CodeGeneration should work." );
            var map = skipEmbeddedStObjMap ? null : r.EmbeddedStObjMap;
            if( map == null )
            {
                var a = Assembly.Load( new AssemblyName( r.AssemblyName ) );
                map = StObjContextRoot.Load( a, TestHelper.Monitor );
                map.Should().NotBeNull();
            }
            return (r.Collector, map!);
        }

        /// <summary>
        /// Compiles from a successful <see cref="StObjCollectorResult"/>. <see cref="StObjCollectorResult.HasFatalError"/> must be
        /// false otherwise an <see cref="ArgumentException"/> is thrown.
        /// <para>
        /// The assembly name is <c>CK.StObj.AutoAssembly + DateTime.Now.ToString( ".yyMdHmsffff" )</c>.
        /// </para>
        /// </summary>
        /// <param name="result">The collector result.</param>
        /// <param name="engineConfigurator">
        /// Optional hook to configure the <see cref="StObjEngineConfiguration"/> or to substitute it by a new one.
        /// <para>
        /// Should be used to add <see cref="StObjEngineConfiguration.Aspects"/> and configure
        /// the available <see cref="BinPathConfiguration"/> in <see cref="StObjEngineConfiguration.BinPaths"/>.
        /// </para>
        /// <para>
        /// Other BinPaths can be added with the same <see cref="BinPathConfiguration.Path"/> as the default one
        /// (this path is <see cref="IBasicTestHelper.TestProjectFolder"/>) but care should be taken with their
        /// configurations.
        /// </para>
        /// </param>
        /// <param name="skipEmbeddedStObjMap">
        /// True to skip any available StObjMap: this MUST be true when
        /// a setup depends on externally injected services.
        /// </param>
        /// <returns>The (successful) collector result and generation code result (that may be in error).</returns>
        static GenerateCodeResult DoGenerateCode( StObjCollectorResult result,
                                                  Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                  bool skipEmbeddedStObjMap )
        {
            Throw.CheckArgument( !result.HasFatalError );
            var assemblyName = StObjContextRoot.GeneratedAssemblyName + DateTime.Now.ToString( ".yyMdHmsffff" );

            var config = new StObjEngineConfiguration()
            {
                GeneratedAssemblyName = assemblyName,
            };
            config.BinPaths.Add( new BinPathConfiguration()
            {
                CompileOption = CompileOption.Compile,
                GenerateSourceFiles = !skipEmbeddedStObjMap,
                ProjectPath = TestHelper.TestProjectFolder
            } );
            if( engineConfigurator != null )
            {
                config = engineConfigurator.Invoke( config );
                Throw.CheckState( "The returned engine configuration cannot be null.", config != null );
            }
            var success = CK.Setup.StObjEngine.Run( TestHelper.Monitor, result, config );

            if( skipEmbeddedStObjMap || !success ) return new GenerateCodeResult( result, success, null, assemblyName );

            IStObjMap? embedded = StObjContextRoot.Load( config.BaseSHA1, TestHelper.Monitor );
            if( embedded != null )
            {
                TestHelper.Monitor.Info( embedded == null ? "No embedded generated source code." : "Embedded generated source code is available." );
            }
            return new GenerateCodeResult( result, success, embedded, assemblyName );
        }

        AutoServiceResult IStObjEngineTestHelperCore.CreateAutomaticServices( StObjCollector c,
                                                                              Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                              SimpleServiceContainer? startupServices,
                                                                              Action<StObjContextRoot.ServiceRegister>? configureServices )
        {
            var (result, map) = DoCompileAndLoadStObjMap( c, engineConfigurator, skipEmbeddedStObjMap: startupServices != null );
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );
            configureServices?.Invoke( reg );
            return (result, map, reg, reg.Services.BuildServiceProvider());
        }

        StObjContextRoot.ServiceRegister IStObjEngineTestHelperCore.GetFailedAutomaticServicesConfiguration( StObjCollector c,
                                                                                                             Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                                                             SimpleServiceContainer? startupServices )
        {
            IStObjMap map = DoCompileAndLoadStObjMap( c, engineConfigurator, skipEmbeddedStObjMap: startupServices != null ).Map;
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
