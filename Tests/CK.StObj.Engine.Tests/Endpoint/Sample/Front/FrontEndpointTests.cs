using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{

    [TestFixture]
    public partial class FrontEndpointTests
    {
        [Test]
        public async Task global_DI_automatically_falls_back_to_default_value_provider_for_ubiquitous_info_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( FakeTenantInfo ),
                                                     typeof( DefaultTenantProvider ) );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Request monitor" ) );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );
            using( var scoped = services.CreateScope() )
            {
                var tenant = scoped.ServiceProvider.GetService<IFakeTenantInfo>();
                Debug.Assert( tenant != null );
                tenant.Name.Should().Be( "DefaultTenant" );
            }
        }
    }
}
