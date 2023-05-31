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
        sealed class AuthInfo : IFakeAuthenticationInfo
        {
            public int ActorId { get; set; }

            public string UserName { get; set; }
        }

        [Test]
        public async Task Background_execution_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SampleCommandProcessor ),
                                                     typeof( BackgroundEndpointDefinition ),
                                                     typeof( BackgroundExecutor ),
                                                     typeof( SampleCommandMemory ) );
            await using var services = await TestHelper.StartHostedServicesAsync( TestHelper.CreateAutomaticServices( c ).Services );

            var user1 = new AuthInfo() { ActorId = 1, UserName = "One" };
            var user2 = new AuthInfo() { ActorId = 2, UserName = "Two" };

            var endpoint = services.GetRequiredService<BackgroundExecutor>();
            endpoint.Push( user1, "A command." );
            endpoint.Push( user2, "Another command." );
            endpoint.Stop();
            await endpoint.WaitForTerminationAsync();

            var history = services.GetRequiredService<SampleCommandMemory>();
            history.ExecutionTrace.Should().HaveCount( 2 ).And.Contain( "A command. - One - 1", "Another command. - Two - 2" );
        }
    }
}
