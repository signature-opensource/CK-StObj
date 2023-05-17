using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Reflection;
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

        // We allow IScopedAutoService for the lifetime.
        // This is no more a auto service because it is an endpoint service.
        [EndpointScopedService( typeof( DefaultEndpointDefinition ) )]
        public interface IEPService1 : IScopedAutoService
        {
        }

        [EndpointScopedService( typeof( AnotherEndpointDefinition ) )]
        public interface IEPService2 : IEPService1
        {
        }

        [Test]
        public void service_availability_does_not_propagate_through_inheritance()
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
            r.EndpointContexts[1].ScopedServices.Should().HaveCount( 1 ).And.Contain( new[] { typeof( IEPService2 ) } );
        }


    }
}
