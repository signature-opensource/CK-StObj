using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Testing
{
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


        public static ServicedApplication CreateServicedApplication( this IStObjMap map,
                                                                     HostApplicationBuilderSettings? hostBuilderSettings = null,
                                                                     Action<IServiceCollection>? configureServices = null,
                                                                     SimpleServiceContainer? startupServices = null )
        {
            var builder = Host.CreateEmptyApplicationBuilder( hostBuilderSettings ?? new HostApplicationBuilderSettings { DisableDefaults = true } );

            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, builder.Services, startupServices );
            configureServices?.Invoke( builder.Services );
            reg.AddStObjMap( map ).Should().BeTrue( "Service configuration succeed." );

            var host = builder.Build();
            // Getting the IHostedService is enough to initialize the DI containers.
            // Even if IHost.StartAsync() is not called, the DI containers are correctly initialized.
            host.Services.GetServices<IHostedService>();
            return new ServicedApplication( host );
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

    }
}
