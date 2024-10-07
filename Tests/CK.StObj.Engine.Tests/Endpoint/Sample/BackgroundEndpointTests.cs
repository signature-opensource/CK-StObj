using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.MonitorTestHelper;
using Microsoft.Extensions.Hosting;

namespace CK.StObj.Engine.Tests.Endpoint;


[TestFixture]
public partial class BackgroundEndpointTests
{

    // Because IFakeTenantInfo/FakeTenantInfo is a IAutoService, registering the resolution
    // of one of it is enough (but both can be registered).
    [TestCase( "Register IFakeTenantInfo" )]
    [TestCase( "Register FakeTenantInfo" )]
    [TestCase( "Register both" )]
    public async Task Background_execution_Async( string mode )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( DefaultCommandProcessor ),
                                        typeof( BackgroundDIContainerDefinition ),
                                        typeof( BackgroundExecutorService ),
                                        typeof( SampleCommandMemory ),
                                        typeof( TenantResolutionService ),
                                        typeof( FakeTenantInfo ),
                                        typeof( DefaultTenantProvider ),
                                        typeof( TransactionalCallContextLike ) );
        using var auto = configuration.Run().CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Request monitor" ) );
            services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            if( mode != "Register FakeTenantInfo" )
            {
                services.AddScoped<IFakeTenantInfo>( sp => sp.GetRequiredService<TenantResolutionService>().GetTenantFromRequest() );
            }
            if( mode != "Register IFakeTenantInfo" )
            {
                services.AddScoped<FakeTenantInfo>( sp => (FakeTenantInfo)sp.GetRequiredService<TenantResolutionService>().GetTenantFromRequest() );
            }
        } );

        // In-line execution of a request.
        using( var scoped = auto.Services.CreateScope() )
        {
            scoped.ServiceProvider.GetRequiredService<DefaultCommandProcessor>().Process( command: "In-line" );
        }

        // BackgroundExecutorService is a singleton. We can retrieve it from the root services.
        var backExecutor = auto.Services.GetRequiredService<BackgroundExecutorService>();
        backExecutor.CheckBackgroundServices = sp =>
        {
            var iTenantInfo = sp.GetService( typeof( IFakeTenantInfo ) );
            var cTenantInfo = sp.GetService( typeof( FakeTenantInfo ) );
            var ambientHub = sp.GetService( typeof( AmbientServiceHub ) );

            ambientHub.Should().NotBeNull();
            iTenantInfo.Should().NotBeNull();
            cTenantInfo.Should().NotBeNull();
            iTenantInfo.Should().BeSameAs( cTenantInfo );
        };
        backExecutor.Start();

        // Background execution of a request.
        using( var scoped = auto.Services.CreateScope() )
        {
            var ubiq = scoped.ServiceProvider.GetRequiredService<AmbientServiceHub>();
            backExecutor.Push( TestHelper.Monitor, ubiq, command: "Background" );
        }

        // Background execution of a request with an overridden tenant.
        using( var scoped = auto.Services.CreateScope() )
        {
            var ubiq = scoped.ServiceProvider.GetRequiredService<AmbientServiceHub>();
            ubiq.IsLocked.Should().BeTrue();
            ubiq = ubiq.CleanClone();
            ubiq.Override( new FakeTenantInfo( "AnotherTenant" ) );
            backExecutor.Push( TestHelper.Monitor, ubiq, command: "Background in another tenant" );
        }

        backExecutor.Stop();
        await backExecutor.WaitForTerminationAsync();

        var history = auto.Services.GetRequiredService<SampleCommandMemory>();
        history.ExecutionTrace.Should().HaveCount( 3 ).And.Contain( "In-line - AcmeCorp - Request monitor",
                                                                    "Background - AcmeCorp - Runner monitor",
                                                                    "Background in another tenant - AnoherTenant - Runner monitor" );
    }


    [Test]
    public async Task IOptions_in_the_background_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SampleCommandProcessorWithOptions ),
                                              typeof( SampleCommandMemory ),
                                              typeof( BackgroundDIContainerDefinition ),
                                              typeof( BackgroundExecutorService ) );
        await using var app = configuration.Run().LoadMap().CreateServicedApplication( configureServices: services =>
        {
            services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Front monitor" ) );
            services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            OptionsServiceCollectionExtensions.AddOptions( services );
            services.Configure<SomeCommandProcessingOptions>( o => o.Power = 42 );
        } );

        await app.StartAsync();

        // BackgroundExecutor is a singleton. We can retrieve it from the root services.
        var backExecutor = app.Services.GetRequiredService<BackgroundExecutorService>();
        backExecutor.Start();
        using( var scoped = app.Services.CreateScope() )
        {
            var ubiq = scoped.ServiceProvider.GetRequiredService<AmbientServiceHub>();
            await backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptions>() );
        }
        var history = app.Services.GetRequiredService<SampleCommandMemory>();
        history.ExecutionTrace.Should().HaveCount( 1 ).And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptions> - 42" );

    }

    [Test]
    public async Task IOptionsSnapshot_in_the_background_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SampleCommandProcessorWithOptionsSnapshot ),
                                        typeof( SampleCommandMemory ),
                                        typeof( BackgroundDIContainerDefinition ),
                                        typeof( BackgroundExecutorService ) );

        ConfigurationManager config = new ConfigurationManager();
        config.AddInMemoryCollection( new Dictionary<string, string?> { { "Opt:Power", "3712" } } );
        await using var app = configuration.Run().LoadMap().CreateServicedApplication( configureServices: services =>
        {
            services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Front monitor" ) );
            services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

            OptionsServiceCollectionExtensions.AddOptions( services );
            services.Configure<SomeCommandProcessingOptions>( config.GetRequiredSection( "Opt" ) );
        } );

        await app.StartAsync();

        // BackgroundExecutor and SampleCommandMemory are singletons. We can retrieve them from the root services.
        var history = app.Services.GetRequiredService<SampleCommandMemory>();
        var backExecutor = app.Services.GetRequiredService<BackgroundExecutorService>();
        backExecutor.Start();

        using( var scoped = app.Services.CreateScope() )
        {
            var ubiq = scoped.ServiceProvider.GetRequiredService<AmbientServiceHub>();
            await backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot>() );
        }
        history.ExecutionTrace.Should().HaveCount( 1 ).And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot> - 3712" );

        config.AddInMemoryCollection( new Dictionary<string, string?> { { "Opt:Power", "42" } } );

        using( var scoped = app.Services.CreateScope() )
        {
            var ubiq = scoped.ServiceProvider.GetRequiredService<AmbientServiceHub>();
            await backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot>() );
        }
        history.ExecutionTrace.Should().HaveCount( 2 )
            .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot> - 3712" )
            .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot> - 42" );

    }

    [Test]
    public async Task IOptionsMonitor_in_the_background_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SampleCommandProcessorWithOptionsMonitor ),
                                        typeof( SampleCommandMemory ),
                                        typeof( BackgroundDIContainerDefinition ),
                                        typeof( BackgroundExecutorService ) );

        ConfigurationManager config = new ConfigurationManager();
        config.AddInMemoryCollection( new Dictionary<string, string?> { { "Opt:Power", "3712" } } );
        var hostBuilderSetting = new HostApplicationBuilderSettings() { Configuration = config, DisableDefaults = true };
        await using var app = configuration.Run().LoadMap().CreateServicedApplication( hostBuilderSetting, configureServices: services =>
        {
            services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Front monitor" ) );
            services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

            OptionsServiceCollectionExtensions.AddOptions( services );
            services.Configure<SomeCommandProcessingOptions>( config.GetRequiredSection( "Opt" ) );
        } );

        await app.StartAsync();

        // Just to check that the configuration is reactive.
        var o = app.Services.GetRequiredService<IOptionsMonitor<SomeCommandProcessingOptions>>();
        o.CurrentValue.Power.Should().Be( 3712 );
        config.AddInMemoryCollection( new Dictionary<string, string?> { { "Opt:Power", "0" } } );
        await Task.Delay( 100 );
        o.CurrentValue.Power.Should().Be( 0 );

        // BackgroundExecutor and SampleCommandMemory are singletons. We can retrieve them from the root services.
        var history = app.Services.GetRequiredService<SampleCommandMemory>();
        var backExecutor = app.Services.GetRequiredService<BackgroundExecutorService>();
        backExecutor.Start();

        config.GetRequiredSection( "Opt" ).GetReloadToken().RegisterChangeCallback( _ => ActivityMonitor.StaticLogger.Info( "Configuration changed!" ), null );
        using( var scoped = app.Services.CreateScope() )
        {
            var ubiq = scoped.ServiceProvider.GetRequiredService<AmbientServiceHub>();
            var t = backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsMonitor>() );
            await Task.Delay( 100 );
            config.AddInMemoryCollection( new Dictionary<string, string?> { { "Opt:Power", "42" } } );
            await t;
        }
        history.ExecutionTrace.Should().HaveCount( 2 )
            .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsMonitor> - 0" )
            .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsMonitor> - 42" );

    }

}
