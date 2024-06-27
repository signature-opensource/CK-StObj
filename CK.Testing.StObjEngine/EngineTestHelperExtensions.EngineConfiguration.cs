using CK.Core;
using CK.Setup;
using CK.Testing;
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
        /// Calls the <see cref="CKEngine.Run(EngineConfiguration, IActivityMonitor)"/> on the <see cref="Monitoring.IMonitorTestHelperCore.Monitor"/>.
        /// </summary>
        /// <param name="configuration">This configuration.</param>
        /// <returns>The engine result.</returns>
        public static EngineResult Run( this EngineConfiguration configuration )
        {
            // Temporary workaround: the GeneratedAssemblyName must be based on the Signature
            // This means that Aspects must be able to impact the Signature in their initialization
            // easily (Apsects should be refacored with template method).
            // Future:
            //  - GeneratedAssemblyName = EngineConfiguration.GeneratedAssemblyNamePrefix.Signature[base64Url].dll
            //  - The code that computes the SHA1 based on the generated code can be removed.
            //  - Finding an external StObjMap is easy (File.Exists in the AppContext.BaseDirectory). 
            configuration.GeneratedAssemblyName = EngineConfiguration.GeneratedAssemblyNamePrefix + DateTime.UtcNow.ToString( ".yMMdHHmsfffff" );
            var r = configuration.Run( TestHelper.Monitor );
            r.Should().NotBeNull( "The configuration is invalid." );
            return r!;
        }

        /// <summary>
        /// Calls the <see cref="CKEngine.Run(EngineConfiguration, IActivityMonitor)"/> on the <see cref="Monitoring.IMonitorTestHelperCore.Monitor"/>.
        /// </summary>
        /// <param name="configuration">This configuration.</param>
        /// <param name="allowSkippedRun">False to forbid a <see cref="RunStatus.Skipped"/>.</param>
        /// <returns>The engine result.</returns>
        public static EngineResult RunSuccessfully( this EngineConfiguration configuration, bool allowSkippedRun = true )
        {
            var r = Run( configuration );
            if( allowSkippedRun )
            {
                if( r.Status is RunStatus.Failed )
                {
                    Throw.CKException( $"Engine run failed." );
                }
                TestHelper.Monitor.Info( $"Engine status '{r.Status}'." );
            }
            else
            {
                if( r.Status is not RunStatus.Succeed )
                {
                    Throw.CKException( $"Engine run status '{r.Status}'." );
                }
            }
            return r;
        }

        /// <summary>
        /// Attempts to build and configure a IServiceProvider from the specified <paramref name="binPathName"/> and ensures
        /// that this fails while configuring the Services (and not before).
        /// </summary>
        /// <param name="configuration">This configuration.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
        /// <param name="configureServices">Optional services configuration.</param>
        /// <param name="binPathName">The <see cref="BinPathConfiguration.Name"/>. Must be an existing BinPath or a <see cref="ArgumentException"/> is thrown.</param>
        public static void GetFailedAutomaticServices( this EngineConfiguration configuration,
                                                       string message,
                                                       IEnumerable<string>? otherMessages = null,
                                                       Action<IServiceCollection>? configureServices = null,
                                                       SimpleServiceContainer? startupServices = null,
                                                       string binPathName = "First" )
        {
            using( TestHelper.Monitor.CollectEntries( out var entries ) )
            {
                bool loadMapSucceed = false;
                bool addedStobjMapSucceed = false;
                var engineResult = configuration.Run();
                if( engineResult.Status == RunStatus.Succeed )
                {
                    var map = engineResult.FindRequiredBinPath( binPathName ).TryLoadMap( TestHelper.Monitor );
                    if( map != null )
                    {
                        loadMapSucceed = true;
                        var services = new ServiceCollection();
                        var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, services, startupServices );
                        configureServices?.Invoke( services );
                        addedStobjMapSucceed = reg.AddStObjMap( map );

                        using var serviceProvider = reg.Services.BuildServiceProvider();
                        // Getting the IHostedService is enough to initialize the DI containers.
                        serviceProvider.GetServices<IHostedService>();
                    }
                }
                CheckExpectedMessages( entries.Select( e => e.Text + CKExceptionData.CreateFrom( e.Exception )?.ToString() ), message, otherMessages );
                addedStobjMapSucceed.Should().BeFalse( loadMapSucceed
                                                         ? $"Service configuration ({nameof( StObjContextRoot.ServiceRegister.AddStObjMap )}) failed."
                                                         : engineResult.Status == RunStatus.Succeed
                                                            ? "LoadStObjMap failed."
                                                            : "Code generation failed." );
            }
        }

        /// <summary>
        /// <see cref="TypeConfigurationSet.Add(Type)"/> any number of types.
        /// </summary>
        /// <param name="set">This type configuration set.</param>
        /// <param name="types">Types to add.</param>
        /// <returns>This set.</returns>
        public static TypeConfigurationSet Add( this TypeConfigurationSet set, IEnumerable<Type> types )
        {
            foreach( var t in types ) set.Add( t );
            return set;
        }

        /// <inheritdoc cref="Add(TypeConfigurationSet,IEnumerable{Type})"/> 
        public static TypeConfigurationSet Add( this TypeConfigurationSet set, params Type[] types )
        {
            foreach( var t in types ) set.Add( t );
            return set;
        }

    }
}
