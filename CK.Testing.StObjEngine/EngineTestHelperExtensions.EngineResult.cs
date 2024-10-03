using CK.AppIdentity;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Testing;

public static partial class EngineTestHelperExtensions
{
    /// <summary>
    /// Loads the <see cref="IStObjMap"/>. Must be called only if <see cref="Success"/> is true.
    /// </summary>
    /// <returns>The map.</returns>
    public static IStObjMap LoadMap( this EngineResult.BinPath binPath ) => binPath.LoadMap( TestHelper.Monitor );

    /// <summary>
    /// Loads the <see cref="IStObjMap"/> for the specified BinPath. Must be called only if <see cref="Success"/> is true.
    /// </summary>
    /// <param name="binPathName">The bin path name. Must be an existing BinPath or a <see cref="ArgumentException"/> is thrown.</param>
    /// <returns>The map.</returns>
    public static IStObjMap LoadMap( this EngineResult engineResult, string binPathName = "First" ) => engineResult.FindRequiredBinPath( binPathName )
                                                                                                                   .LoadMap();
    /// <summary>
    /// Fully builds and configures a IServiceProvider after a successful run of the engine fully configured service provider
    /// in <see cref="AutomaticServices"/> (that must be disposed).
    /// <para>
    /// The G0.cs file is updated and the assembly is generated. If the StObjMap is already loaded and available, it is chosen: the second run of a
    /// test can debug the generated code by putting breakpoints in the G0.cs file and this file can be freely modified as long as the first line
    /// with the signature is not altered.
    /// </para>
    /// </summary>
    /// <param name="map">The CKomposable map.</param>
    /// <param name="configureServices">Optional services configurator.</param>
    /// <param name="startupServices">Optional startup services (used to configure the Real Objects when building the service provider).</param>
    /// <returns>The fully configured service provider.</returns>
    public static AutomaticServices CreateAutomaticServices( this IStObjMap map,
                                                             Action<IServiceCollection>? configureServices = null,
                                                             SimpleServiceContainer? startupServices = null )
    {
        var services = new ServiceCollection();
        var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, services, startupServices );
        configureServices?.Invoke( services );
        reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );

        var serviceProvider = reg.Services.BuildServiceProvider();
        // Getting the IHostedService is enough to initialize the DI containers.
        serviceProvider.GetServices<IHostedService>();
        return new AutomaticServices( map, serviceProvider, reg.Services );
    }

    /// <summary>
    /// Fully builds and configures a IServiceProvider after a successful run of the engine fully configured service provider
    /// in <see cref="AutomaticServices"/> (that must be disposed).
    /// <para>
    /// The G0.cs file is updated and the assembly is generated. If the StObjMap is already loaded and available, it is chosen: the second run of a
    /// test can debug the generated code by putting breakpoints in the G0.cs file and this file can be freely modified as long as the first line
    /// with the signature is not altered.
    /// </para>
    /// </summary>
    /// <param name="engineResult">This engine result.</param>
    /// <param name="alterPocoTypeSystem">Optional configurator for the <see cref="IPocoTypeSystemBuilder"/>.</param>
    /// <param name="configureServices">Optional services configurator.</param>
    /// <param name="binPathName">The <see cref="BinPathConfiguration.Name"/>. Must be an existing BinPath or a <see cref="ArgumentException"/> is thrown.</param>
    /// <returns>The fully configured service provider.</returns>
    public static AutomaticServices CreateAutomaticServices( this EngineResult engineResult,
                                                             Action<IPocoTypeSystemBuilder>? alterPocoTypeSystem = null,
                                                             Action<IServiceCollection>? configureServices = null,
                                                             string binPathName = "First" )
    {
        var b = engineResult.FindRequiredBinPath( binPathName );
        var pocoTypeSystem = b.PocoTypeSystemBuilder;
        if( pocoTypeSystem?.IsLocked is false ) alterPocoTypeSystem?.Invoke( pocoTypeSystem );
        return b.LoadMap( TestHelper.Monitor ).CreateAutomaticServices( configureServices );
    }

    /// <summary>
    /// Creates a <see cref="ServicedApplication"/> or a <see cref="AppIdentityApplication"/> if <see cref="ApplicationIdentityService"/>
    /// is available in the services.
    /// <para>
    /// If the <see cref="HostApplicationBuilderSettings.Configuration"/> contains a "CK-AppIdentity" section, a <see cref="ApplicationIdentityServiceConfiguration"/>
    /// is built from it and registered in the services. Thanks to this, if the <see cref="ApplicationIdentityService"/> is registered (either in this <paramref name="map"/>
    /// or by <paramref name="configureServices"/>), it can be configured and a <see cref="AppIdentityApplication"/> is returned.
    /// </para>
    /// <para>
    /// The application must be be disposed once done with it.
    /// </para>
    /// </summary>
    /// <param name="map">This CKomposable map.</param>
    /// <param name="hostBuilderSettings">Optional host settings. <see cref="HostApplicationBuilderSettings.DisableDefaults"/> is set to true by default.</param>
    /// <param name="configureServices">The configure services call back.</param>
    /// <param name="useTestAppIdentityStore">
    /// By default if a "CK-AppIdentity" section exists with an empty or missing "StoreRootPath" key, it is set
    /// to "<see cref="IBasicTestHelper.TestProjectFolder"/>/CK-AppIdentity-Store".
    /// </param>
    /// <param name="startupServices">Optional startup services.</param>
    /// <returns>A ready to be started application.</returns>
    public static ServicedApplication CreateServicedApplication( this IStObjMap map,
                                                                 HostApplicationBuilderSettings? hostBuilderSettings = null,
                                                                 Action<IServiceCollection>? configureServices = null,
                                                                 bool useTestAppIdentityStore = true,
                                                                 SimpleServiceContainer? startupServices = null )
    {
        var builder = Host.CreateEmptyApplicationBuilder( hostBuilderSettings ?? new HostApplicationBuilderSettings { DisableDefaults = true } );
        var appIdentitySection = builder.Configuration.GetSection( "CK-AppIdentity" );
        if( appIdentitySection.Exists() )
        {
            Throw.DebugAssert( nameof( ApplicationIdentityServiceConfiguration.StoreRootPath ) == "StoreRootPath" );
            if( useTestAppIdentityStore && string.IsNullOrWhiteSpace( appIdentitySection["StoreRootPath"] ) )
            {
                appIdentitySection["StoreRootPath"] = TestHelper.TestProjectFolder.AppendPart( "CK-AppIdentity-Store" );
            }
            var defaultDomain = new NormalizedPath( "Test" ).AppendPart( TestHelper.SolutionName );
            var c = ApplicationIdentityServiceConfiguration.Create( TestHelper.Monitor, appIdentitySection, defaultDomain, TestHelper.TestProjectName );
            if( c == null )
            {
                Throw.ArgumentException( $"Invalid \"CK-AppIdentity\" section." );
            }
            builder.Services.AddSingleton( c );
        }
        var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, builder.Services, startupServices );
        configureServices?.Invoke( builder.Services );
        reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );

        var host = builder.Build();
        // Getting the IHostedService is enough to initialize the DI containers.
        // Even if IHost.StartAsync() is not called, the DI containers are correctly initialized.
        host.Services.GetServices<IHostedService>();
        var appIdentity = host.Services.GetService<ApplicationIdentityService>();
        return appIdentity == null ? new ServicedApplication( host ) : new AppIdentityApplication( host, appIdentity );
    }

    /// <summary>
    /// Creates a <see cref="AppIdentityApplication"/>. The <see cref="ApplicationIdentityService"/> must be registered (either in this <paramref name="map"/>
    /// or by <paramref name="configureServices"/>) and the <paramref name="appIdentityConfiguration"/> must produce a valid CK-AppIdentity configuration
    /// otherwise an <see cref="ArgumentException"/> is trown.
    /// <para>
    /// The application must be be disposed once done with it.
    /// </para>
    /// </summary>
    /// <param name="map">This CKomposable map.</param>
    /// <param name="appIdentityConfiguration">Optional host settings. <see cref="HostApplicationBuilderSettings.DisableDefaults"/> is set to true by default.</param>
    /// <param name="configureServices">The configure services call back.</param>
    /// <param name="useTestAppIdentityStore">
    /// By default if a "CK-AppIdentity" section exists with an empty or missing "StoreRootPath" key, sets it to
    /// "<see cref="IBasicTestHelper.TestProjectFolder"/>/CK-AppIdentity-Store".
    /// </param>
    /// <param name="disableHostDefaults">See <see cref="HostApplicationBuilderSettings.DisableDefaults"/>.</param>
    /// <param name="startupServices">Optional startup services.</param>
    /// <returns>A ready to be started application.</returns>
    public static AppIdentityApplication CreateAppIdentityApplication( this IStObjMap map,
                                                                       Action<MutableConfigurationSection> appIdentityConfiguration,
                                                                       Action<IServiceCollection>? configureServices = null,
                                                                       bool useTestAppIdentityStore = true,
                                                                       bool disableHostDefaults = true,
                                                                       SimpleServiceContainer? startupServices = null )
    {
        var config = new MutableConfigurationSection( "CK-AppIdentity" );
        appIdentityConfiguration( config );
        var hostBuilderSettings = new HostApplicationBuilderSettings
        {
            DisableDefaults = disableHostDefaults,
            Configuration = new ConfigurationManager()
        };
        hostBuilderSettings.Configuration.Sources.Add( new ChainedConfigurationSource() { Configuration = config } );
        return (AppIdentityApplication)CreateServicedApplication( map, hostBuilderSettings, configureServices, useTestAppIdentityStore, startupServices );
    }

}
