using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{

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
            var c = TestHelper.CreateStObjCollector( typeof( DefaultCommandProcessor ),
                                                     typeof( BackgroundEndpointDefinition ),
                                                     typeof( BackgroundExecutor ),
                                                     typeof( SampleCommandMemory ),
                                                     typeof( TenantResolutionService ),
                                                     typeof( FakeTenantInfo ),
                                                     typeof( DefaultTenantProvider ) );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Request monitor" ) );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
                if( mode != "Register FakeTenantInfo" )
                {
                    services.Services.AddScoped<IFakeTenantInfo>( sp => sp.GetRequiredService<TenantResolutionService>().GetTenantFromRequest() );
                }
                if( mode != "Register IFakeTenantInfo" )
                {
                    services.Services.AddScoped<FakeTenantInfo>( sp => (FakeTenantInfo)sp.GetRequiredService<TenantResolutionService>().GetTenantFromRequest() );
                }
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );

            // In-line execution of a request.
            using( var scoped = services.CreateScope() )
            {
                scoped.ServiceProvider.GetRequiredService<DefaultCommandProcessor>().Process( command: "In-line" );
            }

            // BackgroundExecutor is a singleton. We can retrieve it from the root services.
            var backExecutor = services.GetRequiredService<BackgroundExecutor>();
            backExecutor.Start();

            // Background execution of a request.
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                backExecutor.Push( TestHelper.Monitor, ubiq, command: "Background" );
            }

            // Background execution of a request with an overridden tenant.
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                ubiq.Override( new FakeTenantInfo( "AntotherTenant" ) );
                backExecutor.Push( TestHelper.Monitor, ubiq, command: "Background in another tenant" );
            }

            backExecutor.Stop();
            await backExecutor.WaitForTerminationAsync();

            var history = services.GetRequiredService<SampleCommandMemory>();
            history.ExecutionTrace.Should().HaveCount( 3 ).And.Contain( "In-line - AcmeCorp - Request monitor",
                                                                        "Background - AcmeCorp - Runner monitor",
                                                                        "Background in another tenant - AnotherTenant - Runner monitor" );
        }


        [Test]
        public async Task IOptions_in_the_background_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SampleCommandProcessorWithOptions ),
                                                     typeof( SampleCommandMemory ),
                                                     typeof( BackgroundEndpointDefinition ),
                                                     typeof( BackgroundExecutor ) );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Front monitor" ) );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
                OptionsServiceCollectionExtensions.AddOptions( services.Services );
                services.Services.Configure<SomeCommandProcessingOptions>( o => o.Power = 42 );
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );

            // BackgroundExecutor is a singleton. We can retrieve it from the root services.
            var backExecutor = services.GetRequiredService<BackgroundExecutor>();
            backExecutor.Start();

            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                await backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptions>() );
            }
            var history = services.GetRequiredService<SampleCommandMemory>();
            history.ExecutionTrace.Should().HaveCount( 1 ).And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptions> - 42" );

        }

        [Test]
        public async Task IOptionsSnapshot_in_the_background_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SampleCommandProcessorWithOptionsSnapshot ),
                                                     typeof( SampleCommandMemory ),
                                                     typeof( BackgroundEndpointDefinition ),
                                                     typeof( BackgroundExecutor ) );
            ConfigurationManager config = new ConfigurationManager();
            config.AddInMemoryCollection( new Dictionary<string, string> { { "Opt:Power", "3712" } } );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Front monitor" ) );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
                
                OptionsServiceCollectionExtensions.AddOptions( services.Services );
                services.Services.Configure<SomeCommandProcessingOptions>( config.GetRequiredSection( "Opt" ) );
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );

            // BackgroundExecutor and SampleCommandMemory are singletond. We can retrieve them from the root services.
            var history = services.GetRequiredService<SampleCommandMemory>();
            var backExecutor = services.GetRequiredService<BackgroundExecutor>();
            backExecutor.Start();

            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                await backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot>() );
            }
            history.ExecutionTrace.Should().HaveCount( 1 ).And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot> - 3712" );

            config.AddInMemoryCollection( new Dictionary<string, string> { { "Opt:Power", "42" } } );

            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                await backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot>() );
            }
            history.ExecutionTrace.Should().HaveCount( 2 )
                .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot> - 3712" )
                .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsSnapshot> - 42" );

        }

        [Test]
        public async Task IOptionsMonitor_in_the_background_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SampleCommandProcessorWithOptionsMonitor ),
                                                     typeof( SampleCommandMemory ),
                                                     typeof( BackgroundEndpointDefinition ),
                                                     typeof( BackgroundExecutor ) );
            ConfigurationManager config = new ConfigurationManager();
            config.AddInMemoryCollection( new Dictionary<string, string> { { "Opt:Power", "3712" } } );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Front monitor" ) );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
                
                OptionsServiceCollectionExtensions.AddOptions( services.Services );
                services.Services.Configure<SomeCommandProcessingOptions>( config.GetRequiredSection( "Opt" ) );
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );

            // Just to check that the configuration is reactive.
            var o = services.GetRequiredService<IOptionsMonitor<SomeCommandProcessingOptions>>();
            o.CurrentValue.Power.Should().Be( 3712 );
            config.AddInMemoryCollection( new Dictionary<string, string> { { "Opt:Power", "0" } } );
            await Task.Delay( 100 );
            o.CurrentValue.Power.Should().Be( 0 );

            // BackgroundExecutor and SampleCommandMemory are singletons. We can retrieve them from the root services.
            var history = services.GetRequiredService<SampleCommandMemory>();
            var backExecutor = services.GetRequiredService<BackgroundExecutor>();
            backExecutor.Start();

            config.GetRequiredSection( "Opt" ).GetReloadToken().RegisterChangeCallback( _ => ActivityMonitor.StaticLogger.Info( "Configuration changed!" ), null );
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                var t = backExecutor.RunAsync( TestHelper.Monitor, ubiq, new CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsMonitor>() );
                await Task.Delay( 100 );
                config.AddInMemoryCollection( new Dictionary<string, string> { { "Opt:Power", "42" } } );
                await t;
            }
            history.ExecutionTrace.Should().HaveCount( 2 )
                .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsMonitor> - 0" )
                .And.Contain( "CommandThatMustBeProcessedBy<SampleCommandProcessorWithOptionsMonitor> - 42" );

        }

    }
}
