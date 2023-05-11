

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
        /// <para>
        /// This methods expects at least a substring that must appear in a Error or Fatal emitted log. Testing a failure
        /// should always challenge that the failure cause is what it should be.
        /// To disable this (but this is NOT recommended), <paramref name="message"/> may be set to the empty string.
        /// </para>
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">Optional fatal messages substring that must be emitted.</param>
        /// <returns>The failed collector result or null if the error prevented its creation.</returns>
        StObjCollectorResult? GetFailedResult( StObjCollector c, string message, params string[] otherMessages );

        /// <summary>
        /// Runs the <see cref="StObjEngine"/> on a <see cref="GetSuccessfulResult(StObjCollector)"/>.
        /// <para>
        /// Source code file G0.cs is not updated by default, and if <paramref name="compileOption"/> is <see cref="CompileOption.None"/>
        /// or <see cref="CompileOption.Parse"/>, no assembly is generated.
        /// </para>
        /// </summary>
        /// <param name="c">The collector.</param>
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
        /// <param name="compileOption">Compilation behavior.</param>
        /// <param name="generateSourceFile">True to update the G0.cs file.</param>
        /// <returns>The (successful) collector result and <see cref="StObjEngineResult"/> (that may be in error).</returns>
        GenerateCodeResult GenerateCode( StObjCollector c,
                                         Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator,
                                         bool generateSourceFile = false,
                                         CompileOption compileOption = CompileOption.None );

        /// <summary>
        /// Compiles and loads the <see cref="IStObjMap"/> from the generated assembly based on
        /// a <see cref="GetSuccessfulResult(StObjCollector)"/>.
        /// <para>
        /// Source code file G0.cs is updated by default and the assembly is generated.
        /// </para>
        /// </summary>
        /// <param name="c">The collector.</param>
        /// <param name="generateSourceFile">False to not update the G0.cs file.</param>
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
        /// <returns>The (successful) result and the ready-to-use map.</returns>
        CompileAndLoadResult CompileAndLoadStObjMap( StObjCollector c,
                                                     bool generateSourceFile = true,
                                                     Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator = null );

        /// <summary>
        /// Fully builds and configures a IServiceProvider after a successful run of the engine and returns all the intermediate results: the (successful) collector
        /// result, the ready-to-use <see cref="IStObjMap"/>, the intermediate service register and the final, fully configured, service provider.
        /// <para>
        /// The G0.cs file is updated and the assembly is generated. If the StObjMap is already loaded and available, it is chosen: the second run of a
        /// test can debug the generated code by putting breakpoints in the G0.cs file and this file can be freely modified as long as the first line
        /// with the signature is not altered.
        /// </para>
        /// <para>
        /// Note that <see cref="AutomaticServicesResult.Services"/> is a <see cref="ServiceProvider"/> that is <see cref="IDisposable"/>: it SHOULD be disposed.
        /// </para>
        /// </summary>
        /// <param name="c">The collector.</param>
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
        /// <param name="startupServices">Optional startup services: see <see cref="StObjContextRoot.ServiceRegister.StartupServices"/>.</param>
        /// <param name="configureServices">Optional services configurator.</param>
        /// <returns>The (successful) collector result, the ready-to-use map, the intermediate service registrar and the final, fully configured, service provider.</returns>
        AutomaticServicesResult CreateAutomaticServices( StObjCollector c,
                                                         Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator = null,
                                                         SimpleServiceContainer? startupServices = null,
                                                         Action<StObjContextRoot.ServiceRegister>? configureServices = null );

        /// <summary>
        /// Attempts to build and configure a IServiceProvider and ensures that this fails while configuring the Services.
        /// </summary>
        /// <param name="c">The collector.</param>
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
        /// <param name="startupServices">Optional startup services: see <see cref="StObjContextRoot.ServiceRegister.StartupServices"/>.</param>
        /// <returns>The (failed) service register.</returns>
        StObjContextRoot.ServiceRegister GetFailedAutomaticServicesConfiguration( StObjCollector c,
                                                                                  Func<StObjEngineConfiguration, StObjEngineConfiguration>? engineConfigurator = null,
                                                                                  SimpleServiceContainer? startupServices = null );

    }
}
