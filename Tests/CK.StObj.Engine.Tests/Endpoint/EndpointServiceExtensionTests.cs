using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [TestFixture]
    public class EndpointServiceExtensionTests
    {
        [EndpointScopedService]
        public interface IEPService1
        {
        }

        [EndpointSingletonService]
        public interface IEPService2 : IAutoService
        {
        }

        [Test]
        public void endpoint_services_are_registered_whether_they_are_IAutoService_or_not()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEPService1 ),
                                                     typeof( IEPService2 ) );
            var r = TestHelper.GetSuccessfulResult( c ).EndpointResult;
            r.EndpointContexts.Should().HaveCount( 0 );
            r.EndpointServices[typeof( IEPService1 )].Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            r.EndpointServices[typeof( IEPService2 )].Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsSingleton | AutoServiceKind.IsAutoService );
        }

        [TEMPEndpointScopedService( isUbiquitousEndpointInfo: true )]
        public sealed class AmbientThing
        {
            public string? ThingName { get; set; }
        }

        public sealed class DefaultAmbientThingProvider : IEndpointUbiquitousServiceDefault<AmbientThing>
        {
            public AmbientThing Default => new AmbientThing() { ThingName = "I'm the default thing name!" };
        }


        [Test]
        public void ubiquitous_info_service_requires_its_default_value_provider()
        {
            var noWay = TestHelper.CreateStObjCollector( typeof( AmbientThing ) );
            TestHelper.GetFailedResult( noWay );

            var c = TestHelper.CreateStObjCollector( typeof( AmbientThing ), typeof( DefaultAmbientThingProvider ) );
            TestHelper.GetSuccessfulResult( c );
        }
    }
}
