using CK.Core;
using CK.Setup;
using CK.Testing.StObjEngine;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Threading;
using static CK.Core.CheckedWriteStream;

namespace CK.Testing
{
    /// <summary>
    /// Extends <see cref="IBasicTestHelper"/> or <see cref="IMonitorTestHelper"/> with engine related helpers.
    /// </summary>
    public static class StObjEngineTestHelperExtensions
    {
        /// <summary>
        /// Creates a new <see cref="TypeCollector"/> and registers the given types into it.
        /// </summary>
        /// <param name="types">The types to register.</param>
        /// <returns>The collector.</returns>
        public static TypeCollector CreateTypeCollector( this IBasicTestHelper helper, params Type[] types )
        {
            var c = new TypeCollector();
            c.AddRange( types );
            return c;
        }

        /// <summary>
        /// Creates a default <see cref="StObjEngineConfiguration"/> with a single BinPath that has its <see cref="BinPathConfiguration.ProjectPath"/> sets
        /// to this <see cref="IBasicTestHelper.TestProjectFolder"/>.
        /// <para>
        /// The <see cref="StObjEngineConfiguration.GeneratedAssemblyName"/> is suffixed with the date time (when using <see cref="CompileOption.Compile"/>).
        /// </para>
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="generateSourceFiles">False to not generate source file.</param>
        /// <param name="compileOption">See <see cref="BinPathConfiguration.CompileOption"/>.</param>
        /// <returns>A new single BinPath configuration.</returns>
        public static StObjEngineConfiguration CreateDefaultEngineConfiguration( this IBasicTestHelper helper, bool generateSourceFiles = true, CompileOption compileOption = CompileOption.Compile )
        {
            var config = new StObjEngineConfiguration()
            {
                GeneratedAssemblyName = StObjContextRoot.GeneratedAssemblyName + DateTime.Now.ToString( ".yyMdHmsffff" )
            };
            config.BinPaths.Add( new BinPathConfiguration()
            {
                CompileOption = compileOption,
                GenerateSourceFiles = generateSourceFiles,
                ProjectPath = helper.TestProjectFolder
            } );
            return config;
        }


        sealed class TypeFilter : IStObjTypeFilter
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

        /// <summary>
        /// Ensures that there is no registration errors at the <see cref="StObjCollector"/> and returns a successful <see cref="StObjCollectorResult"/>.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="typeCollector">The set of types to collect.</param>
        /// <param name="typeFilter">Optional type filter for the <see cref="StObjCollector"/>.</param>
        /// <returns>The successful collector result.</returns>
        public static StObjCollectorResult GetSuccessfulCollectorResult( this IMonitorTestHelper helper, ISet<Type> typeCollector, Func<Type,bool>? typeFilter = null )
        {
            var c = new StObjCollector( new SimpleServiceContainer(), typeFilter: typeFilter != null ? new TypeFilter( typeFilter ) : null );
            c.RegisterTypes( helper.Monitor, typeCollector );
            StObjCollectorResult r = c.GetResult( helper.Monitor );
            r.HasFatalError.Should().Be( false, "There must be no error." );
            return r;
        }

        /// <summary>
        /// Ensures that there are registration errors or a fatal error during the creation of the <see cref="StObjCollectorResult"/>
        /// and returns it if it has been created on error.
        /// <para>
        /// This methods expects at least a substring that must appear in a Error or Fatal emitted log. Testing a failure
        /// should always challenge that the failure cause is what it should be.
        /// To disable this (but this is NOT recommended), <paramref name="message"/> may be set to the empty string.
        /// </para>
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="typeCollector">The set of types to collect.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
        /// <returns>The failed collector result or null if the error prevented its creation.</returns>
        public static StObjCollectorResult? GetFailedCollectorResult( this IMonitorTestHelper helper, ISet<Type> typeCollector, string message, params string[] otherMessages )
        {
            var c = new StObjCollector( new SimpleServiceContainer() );
            c.RegisterTypes( helper.Monitor, typeCollector );
            if( c.FatalOrErrors.Count != 0 )
            {
                helper.Monitor.Error( $"GetFailedCollectorResult: {c.FatalOrErrors.Count} fatal or error during StObjCollector registration." );
                CheckExpectedMessages( c.FatalOrErrors, message, otherMessages );
                return null;
            }
            var r = c.GetResult( helper.Monitor );
            r.HasFatalError.Should().Be( true, "GetFailedCollectorResult: StObjCollector.GetResult() must have failed with at least one fatal error." );
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

        /// <summary>
        /// Runs the engine.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="configuration">The engine configuration.</param>
        /// <param name="types">Types that will be registered in all BinPath.</param>
        /// <returns>The <see cref="StObjEngineResult"/>.</returns>
        public static StObjEngineResult RunStObjEngine( this IMonitorTestHelper helper, StObjEngineConfiguration configuration, ISet<Type> types )
        {
            Throw.CheckNotNullArgument( configuration );
            Throw.CheckNotNullArgument( types );
            var e = new Setup.StObjEngine( helper.Monitor, configuration );
            return e.Run( types );
        }

        /// <summary>
        /// Runs the engine, compiles and loads the <see cref="IStObjMap"/> from the generated assembly or gets the embedded map if it exists.
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <returns>The successful engine result and the ready-to-use map.</returns>
        public static RunAndLoadResult RunSingleBinPathAndLoad( this IMonitorTestHelper helper, ISet<Type> types )
        {
            return RunSingleBinPathAndLoad( helper, CreateDefaultEngineConfiguration( helper ), types );
        }


        /// <summary>
        /// Runs the engine, compiles and loads the <see cref="IStObjMap"/> from the generated assembly or gets the embedded map if it exists.
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="configuration">Engine configuration that must contain a single <see cref="StObjEngineConfiguration.BinPaths"/>.</param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <returns>The successful engine result and the ready-to-use map.</returns>
        public static RunAndLoadResult RunSingleBinPathAndLoad( this IMonitorTestHelper helper,
                                                                StObjEngineConfiguration configuration,
                                                                ISet<Type> types )
        {
            Throw.CheckNotNullArgument( configuration );
            Throw.CheckArgument( configuration.BinPaths.Count == 1 );
            Throw.CheckNotNullArgument( types );
            var e = new Setup.StObjEngine( helper.Monitor, configuration );
            var r = e.Run( types );
            r.Success.Should().BeTrue( "CodeGeneration should work." );
            var map = r.Groups[0].LoadStObjMap( helper.Monitor, embeddedIfPossible: true );
            return new RunAndLoadResult( r, map );
        }

        /// <summary>
        /// Runs the engine, compiles and loads the <see cref="IStObjMap"/> from the generated assembly or gets the embedded map if it exists.
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="configuration">Engine configuration that must contain a single <see cref="StObjEngineConfiguration.BinPaths"/>.</param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <returns>The successful engine result and the ready-to-use map.</returns>
        public static RunAndLoadResult RunSingleBinPathAndLoad( this IMonitorTestHelper helper,
                                                                StObjCollectorResult stObjCollectorResult,
                                                                StObjEngineConfiguration? configuration = null )
        {
            Throw.CheckNotNullArgument( stObjCollectorResult );
            Throw.CheckArgument( configuration == null || configuration.BinPaths.Count == 1 );
            configuration ??= CreateDefaultEngineConfiguration( helper );
            var e = new Setup.StObjEngine( helper.Monitor, configuration );
            var r = e.RunSingleBinPath( stObjCollectorResult );
            r.Success.Should().BeTrue( "CodeGeneration should work." );
            var map = r.Groups[0].LoadStObjMap( helper.Monitor, embeddedIfPossible: true );
            return new RunAndLoadResult( r, map );
        }

        /// <summary>
        /// Fully builds and configures a IServiceProvider after a successful run of the engine and returns all the intermediate results: the (successful) load
        /// result and the final, fully configured, service provider.
        /// <para>
        /// The <see cref="AutomaticServices"/> must be disposed.
        /// </para>
        /// <para>
        /// The G0.cs file is updated and the assembly is generated. If the StObjMap is already loaded and available, it is chosen: the second run of a
        /// test can debug the generated code by putting breakpoints in the G0.cs file and this file can be freely modified as long as the first line
        /// with the signature is not altered.
        /// </para>
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <param name="startupServices">Optional startup services.</param>
        /// <param name="alterPocoTypeSystem">Optional configurator for the <see cref="IPocoTypeSystemBuilder"/>.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <returns>The fully configured service provider.</returns>
        public static AutomaticServices CreateSingleBinPathAutomaticServices( this IMonitorTestHelper helper,
                                                                              ISet<Type> types,
                                                                              SimpleServiceContainer? startupServices = null,
                                                                              Action<IPocoTypeSystemBuilder>? alterPocoTypeSystem = null,
                                                                              Action<StObjContextRoot.ServiceRegister>? configureServices = null )
        {
            return CreateSingleBinPathAutomaticServices( helper, CreateDefaultEngineConfiguration( helper ), types, startupServices, alterPocoTypeSystem, configureServices );
        }

        /// <summary>
        /// Fully builds and configures a IServiceProvider after a successful run of the engine and returns all the intermediate results: the (successful) load
        /// result and the final, fully configured, service provider.
        /// <para>
        /// The <see cref="AutomaticServices"/> must be disposed.
        /// </para>
        /// <para>
        /// The G0.cs file is updated and the assembly is generated. If the StObjMap is already loaded and available, it is chosen: the second run of a
        /// test can debug the generated code by putting breakpoints in the G0.cs file and this file can be freely modified as long as the first line
        /// with the signature is not altered.
        /// </para>
        /// </summary>
        /// <param name="helper"></param>
        /// <param name="configuration">Engine configuration that must contain a single <see cref="StObjEngineConfiguration.BinPaths"/>.</param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <param name="startupServices">Optional startup services.</param>
        /// <param name="alterPocoTypeSystem">Optional configurator for the <see cref="IPocoTypeSystemBuilder"/>.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <returns>The fully configured service provider.</returns>
        public static AutomaticServices CreateSingleBinPathAutomaticServices( this IMonitorTestHelper helper,
                                                                              StObjEngineConfiguration configuration,
                                                                              ISet<Type> types,
                                                                              SimpleServiceContainer? startupServices = null,
                                                                              Action<IPocoTypeSystemBuilder>? alterPocoTypeSystem = null,
                                                                              Action<StObjContextRoot.ServiceRegister>? configureServices = null )
        {
            RunAndLoadResult r = RunSingleBinPathAndLoad( helper, configuration, types );

            var pocoTypeSystem = r.EngineResult.Groups[0].PocoTypeSystemBuilder;
            if( pocoTypeSystem != null ) alterPocoTypeSystem?.Invoke( pocoTypeSystem );

            var services = new ServiceCollection();
            var reg = new StObjContextRoot.ServiceRegister( helper.Monitor, services, startupServices );
            configureServices?.Invoke( reg );
            reg.AddStObjMap( r.Map ).Should().BeTrue( "Service configuration succeed." );

            var serviceProvider = reg.Services.BuildServiceProvider();
            // Getting the IHostedService is enough to initialize the DI containers.
            serviceProvider.GetServices<IHostedService>();
            return new AutomaticServices( r, serviceProvider, reg );
        }

        /// <summary>
        /// Attempts to build and configure a IServiceProvider and ensures that this fails.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
        /// <param name="startupServices">Optional startup services.</param>
        /// <param name="configureServices">Optional services configuration.</param>
        public static void GetFailedSingleBinPathAutomaticServices( this IMonitorTestHelper helper,
                                                                    ISet<Type> types,
                                                                    string message,
                                                                    IEnumerable<string>? otherMessages = null,
                                                                    SimpleServiceContainer? startupServices = null,
                                                                    Action<StObjContextRoot.ServiceRegister>? configureServices = null )
        {
            GetFailedSingleBinPathAutomaticServices( helper, CreateDefaultEngineConfiguration( helper ), types, message, otherMessages, startupServices, configureServices );
        }

        /// <summary>
        /// Attempts to build and configure a IServiceProvider and ensures that this fails while configuring the Services.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="configuration">Engine configuration that must contain a single <see cref="StObjEngineConfiguration.BinPaths"/>.</param>
        /// <param name="types">Types to register in the single BinPath.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
        /// <param name="startupServices">Optional startup services.</param>
        /// <param name="configureServices">Optional services configuration.</param>
        public static void GetFailedSingleBinPathAutomaticServices( this IMonitorTestHelper helper,
                                                                    StObjEngineConfiguration configuration,
                                                                    ISet<Type> types,
                                                                    string message,
                                                                    IEnumerable<string>? otherMessages = null,
                                                                    SimpleServiceContainer? startupServices = null,
                                                                    Action<StObjContextRoot.ServiceRegister>? configureServices = null )
        {
            using( helper.Monitor.CollectEntries( out var entries ) )
            {
                bool loadMapSucceed = false;
                bool addedStobjMapSucceed = false;
                var e = new Setup.StObjEngine( helper.Monitor, configuration );
                var r = e.Run( types );
                if( r.Success )
                {
                    var map = r.Groups[0].TryLoadStObjMap( helper.Monitor, embeddedIfPossible: true );
                    if( map != null )
                    {
                        loadMapSucceed = true;

                        var services = new ServiceCollection();
                        var reg = new StObjContextRoot.ServiceRegister( helper.Monitor, services, startupServices );
                        configureServices?.Invoke( reg );
                        addedStobjMapSucceed = reg.AddStObjMap( map );

                        using var serviceProvider = reg.Services.BuildServiceProvider();
                        // Getting the IHostedService is enough to initialize the DI containers.
                        serviceProvider.GetServices<IHostedService>();
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
        /// Starts any <see cref="IHostedService"/> in <paramref name="services"/>.
        /// </summary>
        /// <param name="services">The service provider.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns>The <paramref name="services"/>.</returns>
        public static async Task<IServiceProvider> StartHostedServicesAsync( this IBasicTestHelper helper, IServiceProvider services, CancellationToken cancellation = default )
        {
            foreach( var service in services.GetServices<IHostedService>() )
            {
                await service.StartAsync( cancellation );
            }
            return services;
        }

        /// <summary>
        /// Stops any <see cref="IHostedService"/> in <paramref name="services"/> and optionally disposes the provider if it is disposable.
        /// </summary>
        /// <param name="services">The service provider.</param>
        /// <param name="disposeServices">True to dispose the <paramref name="services"/>.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns>The awaitable.</returns>
        public static async Task StopHostedServicesAsync( this IBasicTestHelper helper, IServiceProvider services, bool disposeServices = false, CancellationToken cancellation = default )
        {
            foreach( var service in services.GetServices<IHostedService>() )
            {
                await service.StopAsync( cancellation );
            }
            if( disposeServices )
            {
                if( services is IAsyncDisposable dA ) await dA.DisposeAsync();
                else if( services is IDisposable d ) d.Dispose();
            }
        }

    }
}
