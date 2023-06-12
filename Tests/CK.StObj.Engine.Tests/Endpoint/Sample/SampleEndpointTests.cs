using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{

    [TestFixture]
    public partial class SampleEndpointTests
    {
        [EndpointScopedService]
        public sealed class TenantResolutionService : IScopedAutoService
        {
            public IFakeTenantInfo GetTenantFromRequest( /*HttpContext ctx*/ )
            {
                // var tenantId = ctx.Request.QueryString["TenanId"];
                return new FakeTenantInfo( "AcmeCorp" );
            }
        }

        [Test]
        public async Task Background_execution_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SampleCommandProcessor ),
                                                     typeof( BackgroundEndpointDefinition ),
                                                     typeof( BackgroundExecutor ),
                                                     typeof( SampleCommandMemory ),
                                                     typeof( TenantResolutionService ),
                                                     typeof( FakeTenantInfo ) );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor() );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
                services.Services.AddScoped<IFakeTenantInfo>( sp => sp.GetRequiredService<TenantResolutionService>().GetTenantFromRequest() );
                services.Services.AddScoped<FakeTenantInfo>( sp => (FakeTenantInfo)sp.GetRequiredService<TenantResolutionService>().GetTenantFromRequest() );
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );

            // In-line execution of a request.
            using( var scoped = services.CreateScope() )
            {
                scoped.ServiceProvider.GetRequiredService<SampleCommandProcessor>().Process( "Inline" );
            }
            // BackgroundExecutor is a singleton. We can retrieve it from the root services.

            var backExecutor = services.GetRequiredService<BackgroundExecutor>();
            backExecutor.Start();

            // Background execution of a request.
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                backExecutor.Push( ubiq, "Background" );
            }

            // Background execution of a request with an overridden tenant.
            using( var scoped = services.CreateScope() )
            {
                var ubiq = scoped.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
                ubiq.Override( new FakeTenantInfo( "AntotherTenant" ) );
                backExecutor.Push( ubiq, "Background" );
            }

            backExecutor.Stop();
            await backExecutor.WaitForTerminationAsync();

            var history = services.GetRequiredService<SampleCommandMemory>();
            history.ExecutionTrace.Should().HaveCount( 3 ).And.Contain( "Inline - AcmeCorp", "Background - AcmeCorp", "Background - AnotherTenant" );
        }
    }
}
