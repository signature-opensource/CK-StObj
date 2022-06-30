

using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Testing.StObjEngine
{
    /// <summary>
    /// StObjEngine core helper exposes simple methods that enables direct tests of the <see cref="CK.Setup.StObjEngine"/>.
    /// </summary>
    public interface IStObjEngineTestHelperCore
    {
        /// <summary>
        /// Creates a new <see cref="StObjCollector"/> into which types must be registered.
        /// This is a very simple helper that calls <see cref="StObjCollector"/> constructor with sensible default values.
        /// </summary>
        /// <param name="typeFilter">The type filter to use when types will be added.</param>
        /// <returns>The collector.</returns>
        StObjCollector CreateStObjCollector( Func<Type, bool>? typeFilter = null );

        /// <summary>
        /// Creates a new <see cref="StObjCollector"/> with <see cref="CreateStObjCollector(Func{Type, bool})"/> and registers
        /// the given types into it.
        /// </summary>
        /// <param name="types">The types to register.</param>
        /// <returns>The collector.</returns>
        StObjCollector CreateStObjCollector( params Type[] types );

        /// <summary>
        /// Ensures that there is no registration errors in the collector and returns a successful <see cref="StObjCollectorResult"/>.
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <returns>The successful collector result.</returns>
        StObjCollectorResult GetSuccessfulResult( StObjCollector c );

        /// <summary>
        /// Ensures that there are registration errors or a fatal error during the creation of the <see cref="StObjCollectorResult"/>
        /// and returns it if it has been created on error.
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <returns>The failed collector result or null if the error prevented its creation.</returns>
        StObjCollectorResult? GetFailedResult( StObjCollector c );

        /// <summary>
        /// Compiles and instantiates a <see cref="IStObjMap"/> from a <see cref="GetSuccessfulResult(StObjCollector)"/>.
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <param name="engineConfigurator">
        /// Optional hook to configure the <see cref="StObjEngineConfiguration"/>.
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
        /// <returns>The (successful) collector result and the ready-to-use map.</returns>
        (StObjCollectorResult Result, IStObjMap Map) CompileAndLoadStObjMap( StObjCollector c, Action<StObjEngineConfiguration>? engineConfigurator = null );

        /// <summary>
        /// Fully builds and configures a IServiceProvider after a successful <see cref="CompileAndLoadStObjMap(StObjCollector)"/> and returns all
        /// the intermediate results: the (successful) collector result, the ready-to-use <see cref="IStObjMap"/>, the intermediate service registrar
        /// and the final, fully configured, service provider.
        /// <para>
        /// Note that <see cref="AutoServiceResult.Services"/> is a <see cref="ServiceProvider"/> that is <see cref="IDisposable"/>: it SHOULD be disposed.
        /// </para>
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <param name="engineConfigurator">
        /// Optional hook to configure the <see cref="StObjEngineConfiguration"/>.
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
        /// <param name="startupServices">Optional startup services: see <see cref="StObjContextRoot.ServiceRegister.StartupServices"/>.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <returns>The (successful) collector result, the ready-to-use map, the intermediate service registrar and the final, fully configured, service provider.</returns>
        AutoServiceResult CreateAutomaticServices( StObjCollector c,
                                                   Action<StObjEngineConfiguration>? engineConfigurator = null,
                                                   SimpleServiceContainer? startupServices = null,
                                                   Action<StObjContextRoot.ServiceRegister>? configureServices = null );

        /// <summary>
        /// Attempts to build and configure a IServiceProvider and ensures that this fails while configuring the Services.
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <param name="engineConfigurator">
        /// Optional hook to configure the <see cref="StObjEngineConfiguration"/>.
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
        /// <param name="startupServices">Optional startup services: see <see cref="StObjContextRoot.ServiceRegister.StartupServices"/>.</param>
        /// <returns>The (failed) service register.</returns>
        StObjContextRoot.ServiceRegister GetFailedAutomaticServicesConfiguration( StObjCollector c,
                                                                                  Action<StObjEngineConfiguration>? engineConfigurator = null,
                                                                                  SimpleServiceContainer? startupServices = null );
    }
}
