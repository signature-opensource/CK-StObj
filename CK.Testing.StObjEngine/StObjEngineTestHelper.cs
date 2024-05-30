using CK.Core;
using CK.Setup;
using CK.Testing.StObjEngine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

        static StObjCollector DoCreateStObjCollector( Func<Type, bool>? typeFilter )
        {
            return new StObjCollector( new SimpleServiceContainer(),
                                       typeFilter: typeFilter != null ? new TypeFilter( typeFilter ) : null );
        }


        StObjCollector IStObjEngineTestHelperCore.CreateStObjCollector( Func<Type, bool>? typeFilter ) => DoCreateStObjCollector( typeFilter );

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
        }

        static void CheckExpectedMessages( IEnumerable<string> fatalOrErrors, string message, IEnumerable<string>? otherMessages )
        {
            CheckMessage( fatalOrErrors, message );
            if( otherMessages != null )
            {
                foreach( var m in otherMessages ) CheckMessage( fatalOrErrors, m );
            }

            static void CheckMessage( IEnumerable<string> fatalOrErrors, string m )
            {
                if( !String.IsNullOrEmpty( m ) )
                {
                    m = m.ReplaceLineEndings();
                    var errors = fatalOrErrors.Select( m => m.ReplaceLineEndings() );
                    errors.Any( e => e.Contains( m, StringComparison.OrdinalIgnoreCase ) ).Should()
                        .BeTrue( $"Expected '{m}' to be found in: {Environment.NewLine}{errors.Concatenate( Environment.NewLine )}" );
                }
            }
        }

        [Obsolete]
        GenerateCodeResult IStObjEngineTestHelperCore.GenerateCode( StObjCollector c,
                                                                    Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                    bool generateSourceFile,
                                                                    CompileOption compileOption )
        {
            return DoGenerateCode( TestHelper.GetSuccessfulResult( c ), engineConfigurator, generateSourceFile, compileOption );
        }

        [Obsolete]
        CompileAndLoadResult IStObjEngineTestHelperCore.CompileAndLoadStObjMap( StObjCollector c,
                                                                                Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator )
        {
            return DoCompileAndLoadStObjMap( c, engineConfigurator );
        }

        [Obsolete]
        static CompileAndLoadResult DoCompileAndLoadStObjMap( StObjCollector c,
                                                              Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator )
        {
            // If the embeddedStObjMap must be used, we update the G0.cs file, but if the StObjMap must be loaded from the assembly
            // we avoid updating G0.cs.
            GenerateCodeResult r = DoGenerateCode( TestHelper.GetSuccessfulResult( c ), engineConfigurator, generateSourceFiles: true, CompileOption.Compile );
            r.Success.Should().BeTrue( "CodeGeneration should work." );
            var map = r.EngineResult.Groups[0].LoadStObjMap( TestHelper.Monitor, embeddedIfPossible: true );
            return new CompileAndLoadResult( r, map );
        }

        [Obsolete]
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
            config.AddBinPath( new BinPathConfiguration()
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

        [Obsolete]
        AutomaticServicesResult IStObjEngineTestHelperCore.CreateAutomaticServices( StObjCollector c,
                                                                                    Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                                    Action<IPocoTypeSystemBuilder>? alterPocoTypeSystem,
                                                                                    SimpleServiceContainer? startupServices,
                                                                                    Action<StObjContextRoot.ServiceRegister>? configureServices )
        {
            var loadResult = DoCompileAndLoadStObjMap( c, engineConfigurator );
            alterPocoTypeSystem?.Invoke( loadResult.CollectorResult.PocoTypeSystemBuilder );
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
            configureServices?.Invoke( reg );
            reg.AddStObjMap( loadResult.Map ).Should().BeTrue( "Service configuration succeed." );

            return new AutomaticServicesResult( loadResult, reg, reg.Services.BuildServiceProvider() );
        }

        [Obsolete]
        void IStObjEngineTestHelperCore.GetFailedAutomaticServicesConfiguration( StObjCollector c,
                                                                                 string message,
                                                                                 IEnumerable<string>? otherMessages,
                                                                                 Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                                                                 SimpleServiceContainer? startupServices,
                                                                                 Action<StObjContextRoot.ServiceRegister>? configureServices )
        {
            using( TestHelper.Monitor.CollectEntries( out var entries ) )
            {
                GenerateCodeResult r = DoGenerateCode( TestHelper.GetSuccessfulResult( c ), engineConfigurator, generateSourceFiles: true, CompileOption.Compile );
                bool loadMapSucceed = false;
                bool addedStobjMapSucceed = false; 
                if( r.Success )
                {
                    var map = r.EngineResult.Groups[0].LoadStObjMap( TestHelper.Monitor, embeddedIfPossible: true );
                    if( map != null )
                    {
                        loadMapSucceed = true;
                        var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, new ServiceCollection(), startupServices );
                        configureServices?.Invoke( reg );
                        addedStobjMapSucceed = reg.AddStObjMap( map );
                    }
                }
                CheckExpectedMessages( entries.Select( e => e.Text + CKExceptionData.CreateFrom( e.Exception )?.ToString() ), message, otherMessages );
                addedStobjMapSucceed.Should().BeFalse( loadMapSucceed
                                                         ? "Service configuration (AddStObjMap) failed."
                                                         : r.Success
                                                            ? "LoadStObjMap failed."
                                                            : "Code generation failed." );
            }
        }

        /// <summary>
        /// Gets the <see cref="IStObjEngineTestHelper"/> default implementation.
        /// </summary>
        public static IStObjEngineTestHelper TestHelper => TestHelperResolver.Default.Resolve<IStObjEngineTestHelper>();

    }
}
