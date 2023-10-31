using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Diagnostics;
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
            Debug.Assert( r != null );
            r.EndpointContexts.Should().HaveCount( 0 );
            r.EndpointServices[typeof( IEPService1 )].Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            r.EndpointServices[typeof( IEPService2 )].Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsSingleton | AutoServiceKind.IsAutoService );
        }

        [EndpointSingletonService]
        interface IEPServiceHidden
        {
        }

        [Test]
        public void internal_endpoint_services_that_are_not_IAutoService_are_ignored()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEPService1 ),
                                                     typeof( IEPServiceHidden ) );
            var r = TestHelper.GetSuccessfulResult( c ).EndpointResult;
            Debug.Assert( r != null );
            r.EndpointContexts.Should().HaveCount( 0 );
            r.EndpointServices[typeof( IEPService1 )].Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            r.EndpointServices.Should().NotContainKey( typeof( IEPServiceHidden ) );
        }

        [EndpointScopedService( isUbiquitousEndpointInfo: true )]
        public class AmbientThing
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
            TestHelper.GetFailedResult( noWay, "Type 'AmbientThing' is not a valid Ubiquitous information service, all ubiquitous service must have a default value provider." );

            var c = TestHelper.CreateStObjCollector( typeof( AmbientThing ), typeof( DefaultAmbientThingProvider ) );
            TestHelper.GetSuccessfulResult( c );
        }

        public class SpecAmbientThing : AmbientThing
        {
        }

        public sealed class SpecAmbientThingProvider : IEndpointUbiquitousServiceDefault<SpecAmbientThing>
        {
            public SpecAmbientThing Default => new SpecAmbientThing() { ThingName = "I'm the default (spec) thing name!" };
        }

        [Test]
        public void specialized_ubiquitous_info_service_not_AutoService_cannot_share_the_SpecDefaultProvider()
        {
            var noWay = TestHelper.CreateStObjCollector( typeof( SpecAmbientThing ), typeof( SpecAmbientThingProvider ) );
            TestHelper.GetFailedResult( noWay );
        }

        [EndpointScopedService( isUbiquitousEndpointInfo: true )]
        public class AutoAmbientThing : IAutoService
        {
            public string? ThingName { get; set; }

            // This test that no public constructor is allowed on Endpoint AutoService.
            protected AutoAmbientThing() { }
        }

        public class SpecAutoAmbientThing : AutoAmbientThing
        {
            protected SpecAutoAmbientThing() { }

            public static SpecAutoAmbientThing Create() => new SpecAutoAmbientThing();
        }

        public sealed class SpecAutoAmbientThingProvider : IEndpointUbiquitousServiceDefault<SpecAutoAmbientThing>
        {
            public SpecAutoAmbientThing Default
            {
                get
                {

                    var s = SpecAutoAmbientThing.Create();
                    s.ThingName = "I'm the default (AutoService spec) thing name!";
                    return s;
                }
            }
        }

        [Test]
        public void specialized_ubiquitous_info_AutoServices_can_share_the_SpecDefaultProvider()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SpecAutoAmbientThing ),
                                                     typeof( AutoAmbientThing ),
                                                     typeof( SpecAutoAmbientThingProvider ) );
            TestHelper.GetSuccessfulResult( c );
        }


    }
}
