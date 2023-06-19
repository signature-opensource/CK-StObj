using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
            var c = TestHelper.CreateStObjCollector( typeof( SampleCommandProcessor ),
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
                scoped.ServiceProvider.GetRequiredService<SampleCommandProcessor>().Process( "In-line" );
            }

            // BackgroundExecutor is a singleton. We can retrieve it from the root services.
            var backExecutor = services.GetRequiredService<BackgroundExecutor>();
            backExecutor.Start();

            // Background execution of a request.
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                backExecutor.Push( TestHelper.Monitor, ubiq, "Background" );
            }

            // Background execution of a request with an overridden tenant.
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                ubiq.Override( new FakeTenantInfo( "AntotherTenant" ) );
                backExecutor.Push( TestHelper.Monitor, ubiq, "Background" );
            }

            backExecutor.Stop();
            await backExecutor.WaitForTerminationAsync();

            var history = services.GetRequiredService<SampleCommandMemory>();
            history.ExecutionTrace.Should().HaveCount( 3 ).And.Contain( "In-line - AcmeCorp - Request monitor",
                                                                        "Background - AcmeCorp - Runner monitor",
                                                                        "Background - AnotherTenant - Runner monitor" );
        }
    }
}
