using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.DI
{
    [TestFixture]
    public class EndpointServiceExtensionTests
    {
        [EndpointDefinition]
        public class AnotherEndpointDefinition : EndpointDefinition
        {
        }

        // We allow IScopedAutoService for the lifetime. This is no more
        // a auto service because it is an endpoint service.
        [EndpointAvailableService( typeof( DefaultEndpointDefinition ) )]
        public interface IEPService1 : IScopedAutoService
        {
        }

        [EndpointAvailableService( typeof( AnotherEndpointDefinition ) )]
        public interface IEPService2 : IEPService1
        {
        }

        [Test]
        public void since_Endpoint_erase_IAutoService_they_can_extend_their_endpoints()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AnotherEndpointDefinition ),
                                                     typeof( IEPService1 ),
                                                     typeof( IEPService2 ) );
            var r = TestHelper.GetSuccessfulResult( c ).EndpointResult;
            r.EndpointServices.Should().Contain( new[] { typeof( IEPService1 ), typeof( IEPService2 ) } );
            r.EndpointContexts.Should().HaveCount( 2 );

            r.EndpointContexts[0].EndpointDefinition.ClassType.Should().Be( typeof( DefaultEndpointDefinition ) );
            r.EndpointContexts[0].ScopedServices.Should().HaveCount( 1 ).And.Contain( typeof( IEPService1 ) );

            r.EndpointContexts[1].EndpointDefinition.ClassType.Should().Be( typeof( AnotherEndpointDefinition ) );
            r.EndpointContexts[0].ScopedServices.Should().HaveCount( 2 ).And.Contain( new[] { typeof( IEPService1 ), typeof( IEPService2 ) } );
        }

        /* TODO:
Simulates

[assembly: EndpointServiceTypeAvailability( typeof( ISomeService ), typeof( AnotherEndpointDefinition ) )]

[EndpointSingletonServiceOwner( typeof(DefaultEndpointDefinition), exclusive: true )]
public interface ISomeService { ... } 

[EndpointSingletonServiceOwner( typeof(AnotherEndpointDefinition), exclusive: true )]
public interface ISomeRefinedService : ISomeService { ... } 

        */
    }
}
