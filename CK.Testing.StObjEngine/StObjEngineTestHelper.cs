using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using System.Xml.Linq;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjEngine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        static StObjCollector DoCreateStObjCollector( Func<Type, bool>? typeFilter )
        {
            return new StObjCollector( new SimpleServiceContainer(),
                                       typeFilter: typeFilter != null ? new TypeFilter( typeFilter ) : null );
        }

        StObjCollector IStObjEngineTestHelperCore.CreateStObjCollector( params Type[] types )
        {
            var c = DoCreateStObjCollector( null );
            c.RegisterTypes( _monitor.Monitor, types );
            return c;
        }

        StObjCollectorResult IStObjEngineTestHelperCore.GetSuccessfulResult( StObjCollector c ) => DoGetSuccessfulResult( _monitor.Monitor, c );

        static StObjCollectorResult DoGetSuccessfulResult( IActivityMonitor monitor, StObjCollector c )
        {
            c.FatalOrErrors.Count.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
            StObjCollectorResult? r = c.GetResult( monitor );
            r.HasFatalError.Should().Be( false, "There must be no error." );
            return r;
        }

        StObjCollectorResult? IStObjEngineTestHelperCore.GetFailedResult( StObjCollector c, string message, params string[] otherMessages )
        {
            if( c.FatalOrErrors.Count != 0 )
            {
                TestHelper.Monitor.Error( $"GetFailedResult: {c.FatalOrErrors.Count} fatal or error during StObjCollector registration." );
                CheckExpectedMessages( c.FatalOrErrors, message, otherMessages );
                return null;
            }
            var r = c.GetResult( _monitor.Monitor );
            r.HasFatalError.Should().Be( true, "GetFailedResult: StObjCollector.GetResult() must have failed with at least one fatal error." );
            CheckExpectedMessages( c.FatalOrErrors, message, otherMessages );
            return r;

            static void CheckExpectedMessages( IReadOnlyList<string> fatalOrErrors, string message, string[] otherMessages )
            {
                CheckMessage( fatalOrErrors, message );
                foreach( var m in otherMessages ) CheckMessage( fatalOrErrors, m );

                static void CheckMessage( IReadOnlyList<string> fatalOrErrors, string m )
                {
                    if( !String.IsNullOrEmpty( m ) )
                    {
                        fatalOrErrors.Any( e => e.Contains( m, StringComparison.OrdinalIgnoreCase ) ).Should()
                            .BeTrue( $"Expected '{m}' to be found in: {Environment.NewLine}{fatalOrErrors.Concatenate( Environment.NewLine )}" );
                    }
                }
            }
        }

        GenerateCodeResult IStObjEngineTestHelperCore.GenerateCode( StObjCollector c,
                                                                    Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                    bool generateSourceFile,
                                                                    CompileOption compileOption )
        {
            return DoGenerateCode( TestHelper.GetSuccessfulResult( c ), engineConfigurator, generateSourceFile, compileOption );
        }

        CompileAndLoadResult IStObjEngineTestHelperCore.CompileAndLoadStObjMap( StObjCollector c,
                                                                                bool generateSourceFile,
                                                                                Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator )
        {
            return DoCompileAndLoadStObjMap( c, engineConfigurator, generateSourceFile, useEmbeddedStObjMapIfPossible: false );
        }

        static CompileAndLoadResult DoCompileAndLoadStObjMap( StObjCollector c,
                                                              Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                              bool generateSourceFile,
                                                              bool useEmbeddedStObjMapIfPossible )
        {
            // If the embeddedStObjMap must be used, we update the G0.cs file, but if the StObjMap must be loaded from the assembly
            // we avoid updating G0.cs.
            GenerateCodeResult r = DoGenerateCode( TestHelper.GetSuccessfulResult( c ), engineConfigurator, generateSourceFile, CompileOption.Compile );
            r.Success.Should().BeTrue( "CodeGeneration should work." );
            var map = r.EngineResult.Groups[0].LoadStObjMap( useEmbeddedStObjMapIfPossible );
            return new CompileAndLoadResult( r, map! );
        }

        static GenerateCodeResult DoGenerateCode( StObjCollectorResult result,
                                                  Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                  bool generateSourceFiles,
                                                  CompileOption compileOption )
        {
            Throw.CheckArgument( !result.HasFatalError );
            var assemblyName = StObjContextRoot.GeneratedAssemblyName + DateTime.Now.ToString( ".yyMdHmsffff" );

            var config = new StObjEngineConfiguration()
            {
                GeneratedAssemblyName = assemblyName,
            };
            config.BinPaths.Add( new BinPathConfiguration()
            {
                CompileOption = compileOption,
                GenerateSourceFiles = generateSourceFiles,
                ProjectPath = TestHelper.TestProjectFolder
            } );
            if( engineConfigurator != null )
            {
                config = engineConfigurator.Invoke( config );
                Throw.CheckState( "The engine configuration returned by the engineConfigurator cannot be null.", config != null );
            }
            return new GenerateCodeResult( result, Setup.StObjEngine.Run( TestHelper.Monitor, result, config ) );
        }

        AutomaticServicesResult IStObjEngineTestHelperCore.CreateAutomaticServices( StObjCollector c,
                                                                                    Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                                    SimpleServiceContainer? startupServices,
                                                                                    Action<StObjContextRoot.ServiceRegister>? configureServices )
        {
            var loadResult = DoCompileAndLoadStObjMap( c, engineConfigurator, true, true );

            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            configureServices?.Invoke( reg );
            reg.AddStObjMap( loadResult.Map ).Should().BeTrue( "Service configuration succeed." );

            return new AutomaticServicesResult( loadResult, reg, reg.Services.BuildServiceProvider() );
        }

        StObjContextRoot.ServiceRegister IStObjEngineTestHelperCore.GetFailedAutomaticServicesConfiguration( StObjCollector c,
                                                                                                             Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                                                             SimpleServiceContainer? startupServices,
                                                                                                             Action<StObjContextRoot.ServiceRegister>? configureServices )
        {
            IStObjMap map = DoCompileAndLoadStObjMap( c, engineConfigurator, true, true ).Map;
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            configureServices?.Invoke( reg );
            reg.AddStObjMap( map ).Should().BeFalse( "Service configuration failed." );
            return reg;
        }

        async Task<ServiceProvider> IStObjEngineTestHelperCore.StartHostedServicesAsync( ServiceProvider services, CancellationToken cancellation )
        {
            foreach( var service in services.GetServices<IHostedService>() )
            {
                await service.StartAsync( cancellation );
            }
            return services;
        }

        async Task IStObjEngineTestHelperCore.StopHostedServicesAsync( ServiceProvider services, bool disposeServices, CancellationToken cancellation )
        {
            foreach( var service in services.GetServices<IHostedService>() )
            {
                await service.StopAsync( cancellation );
            }
            if( disposeServices ) await services.DisposeAsync();
        }

        /// <summary>
        /// Gets the <see cref="IStObjEngineTestHelper"/> default implementation.
        /// </summary>
        public static IStObjEngineTestHelper TestHelper => TestHelperResolver.Default.Resolve<IStObjEngineTestHelper>();

    }
}
