using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Testing
{
    /// <summary>
    /// Extends <see cref="IBasicTestHelper"/> or <see cref="IMonitorTestHelper"/> with engine related helpers.
    /// </summary>
    public static class EngineTestHelperExtensions
    {
        /// <summary>
        /// Creates a new <see cref="TypeCollector"/> and registers the given types into it.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="types">The types to register.</param>
        /// <returns>The collector.</returns>
        [Obsolete( "Use the EngineConfiguration.FirstBinPath.Types directly or a HashSet<Type>" )]
        [EditorBrowsable(EditorBrowsableState.Never)]   
        public static TypeCollector CreateTypeCollector( this IBasicTestHelper helper, IEnumerable<Type> types )
        {
            var c = new TypeCollector();
            c.AddRange( types );
            return c;
        }

        /// <inheritdoc cref="CreateTypeCollector(IBasicTestHelper, IEnumerable{Type})"/>
        [Obsolete( "Use the EngineConfiguration.FirstBinPath.Types directly or a HashSet<Type>" )]
        [EditorBrowsable( EditorBrowsableState.Never )]
        public static TypeCollector CreateTypeCollector( this IBasicTestHelper helper, params Type[] types ) => CreateTypeCollector( helper, (IEnumerable<Type>)types );

        /// <summary>
        /// Creates a default <see cref="EngineConfiguration"/> with the <see cref="EngineConfiguration.FirstBinPath"/> that has
        /// its <see cref="BinPathConfiguration.Path"/> set to the <see cref="IBasicTestHelper.ClosestSUTProjectFolder"/> and its
        /// <see cref="BinPathConfiguration.ProjectPath"/> sets to this <see cref="IBasicTestHelper.TestProjectFolder"/>.
        /// <para>
        /// The <see cref="EngineConfiguration.GeneratedAssemblyName"/> is suffixed with the date time (when using <see cref="CompileOption.Compile"/>).
        /// </para>
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="generateSourceFiles">False to not generate source file.</param>
        /// <param name="compileOption">See <see cref="BinPathConfiguration.CompileOption"/>.</param>
        /// <returns>A new single BinPath configuration.</returns>
        public static EngineConfiguration CreateDefaultEngineConfiguration( this IBasicTestHelper helper, bool generateSourceFiles = true, CompileOption compileOption = CompileOption.Compile )
        {
            var config = new EngineConfiguration()
            {
                GeneratedAssemblyName = EngineConfiguration.GeneratedAssemblyNamePrefix + DateTime.UtcNow.ToString( ".yyMdHmsffff" )
            };
            var sutFolder = helper.ClosestSUTProjectFolder;
            config.FirstBinPath.Path = sutFolder.Combine( helper.PathToBin );
            config.FirstBinPath.CompileOption = compileOption;
            config.FirstBinPath.GenerateSourceFiles = generateSourceFiles;
            config.FirstBinPath.ProjectPath = helper.TestProjectFolder;
            return config;
        }

        /// <summary>
        /// Ensures that there is no registration errors at the <see cref="StObjCollector"/> and returns a successful <see cref="StObjCollectorResult"/>.
        /// </summary>
        /// <param name="helper">This helper.</param>
        /// <param name="types">The set of types to collect.</param>
        /// <returns>The successful collector result.</returns>
        public static StObjCollectorResult GetSuccessfulCollectorResult( this IMonitorTestHelper helper, IEnumerable<Type> types )
        {
            var c = new StObjCollector( new SimpleServiceContainer() );
            c.RegisterTypes( helper.Monitor, types );
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
        /// <param name="types">The set of types to collect.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
        /// <returns>The failed collector result or null if the error prevented its creation.</returns>
        public static StObjCollectorResult? GetFailedCollectorResult( this IMonitorTestHelper helper, IEnumerable<Type> types, string message, params string[] otherMessages )
        {
            var c = new StObjCollector();
            c.RegisterTypes( helper.Monitor, types );
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
        /// Starts any <see cref="IHostedService"/> in <paramref name="services"/>.
        /// </summary>
        /// <param name="helper">This helper.</param>
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
        /// <param name="helper">This helper.</param>
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