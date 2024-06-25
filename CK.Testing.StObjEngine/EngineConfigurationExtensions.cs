using CK.Core;
using CK.Setup;
using CK.Testing;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System;
using static CK.Testing.MonitorTestHelper;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace CK.Testing
{
    public static class EngineConfigurationExtensions
    {
        /// <summary>
        /// Calls the <see cref="CKEngine.Run(EngineConfiguration, IActivityMonitor)"/> on the <see cref="Monitoring.IMonitorTestHelperCore.Monitor"/>.
        /// </summary>
        /// <param name="configuration">This configuration.</param>
        /// <returns>The engine result.</returns>
        public static EngineResult Run( this EngineConfiguration configuration )
        {
            var r = configuration.Run( TestHelper.Monitor );
            r.Should().NotBeNull( "The configuration is invalid." );
            return r!;
        }

        /// <summary>
        /// Attempts to build and configure a IServiceProvider and ensures that this fails while configuring the Services.
        /// </summary>
        /// <param name="configuration">This configuration.</param>
        /// <param name="message">Expected error or fatal message substring that must be emitted.</param>
        /// <param name="otherMessages">More fatal messages substring that must be emitted.</param>
        /// <param name="configureServices">Optional services configuration.</param>
        /// <param name="binPathName">The <see cref="BinPathConfiguration.Name"/>. Must be an existing BinPath or a <see cref="ArgumentException"/> is thrown.</param>
        public static void GetFailedSingleBinPathAutomaticServices( this EngineConfiguration configuration,
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
                    var map = engineResult.FindRequiredBinPath( binPathName ).TryLoadStObjMap( TestHelper.Monitor );
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
        /// <see cref="TypeConfigurationSet.Add(Type)"/> any number of types.
        /// </summary>
        /// <param name="set">This type configuration set.</param>
        /// <param name="types">Types to add.</param>
        public static void Add( this TypeConfigurationSet set, IEnumerable<Type> types )
        {
            foreach( var t in types ) set.Add( t );
        }

        /// <inheritdoc cref="Add(TypeConfigurationSet,IEnumerable{Type})"/> 
        public static void Add( this TypeConfigurationSet set, params Type[] types )
        {
            foreach( var t in types ) set.Add( t );
        }

        /// <summary>
        /// <see cref="TypeConfigurationSet.Add(Type)"/> any number of types into <see cref="BinPathConfiguration.Types"/>.
        /// </summary>
        /// <param name="binPath">This BinPath configuration.</param>
        /// <param name="types">Types to add.</param>
        public static void Add( this BinPathConfiguration binPath, IEnumerable<Type> types ) => binPath.Types.Add( types );

        /// <inheritdoc cref="Add(BinPathConfiguration,IEnumerable{Type})"/> 
        public static void Add( this BinPathConfiguration binPath, params Type[] types ) => binPath.Types.Add( types );

    }
}
