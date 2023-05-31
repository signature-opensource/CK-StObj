using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public class ViolatingTrueSingletonRuleTests
    {
        [EndpointDefinition]
        public abstract class AnotherEndpointDefinition : EndpointDefinition<object> 
        {
        }

        [EndpointSingletonService( typeof( DefaultEndpointDefinition ) )]
        public interface ISomeService { }

        [EndpointSingletonService( typeof( AnotherEndpointDefinition ) )]
        public interface ISomeRefinedService : ISomeService { }

        // Test below simulates this attribute: thanks to this, the 2 contexts expose their own
        // ISomeService and ISomeRefinedService singleton instance.
        //
        // [assembly: EndpointSingletonService( typeof( ISomeService ), typeof( AnotherEndpointDefinition ) )]

        [Test]
        public void singletons_service_can_be_different_instances()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AnotherEndpointDefinition ),
                                                     typeof( ISomeRefinedService ),
                                                     typeof( ISomeService ) );
            c.SetEndpointSingletonService( TestHelper.Monitor,
                                           typeof( ISomeService ),
                                           typeof( AnotherEndpointDefinition ) );

            var r = TestHelper.GetSuccessfulResult( c ).EndpointResult;
            Debug.Assert( r != null );

            r.EndpointServices.Should().Contain( new[] { typeof( ISomeService ), typeof( ISomeRefinedService ) } );
            r.EndpointContexts.Should().HaveCount( 2 );
            var defaultContext = r.EndpointContexts[0];
            var anotherContext = r.EndpointContexts[1];

            defaultContext.EndpointDefinition.ClassType.Should().Be( typeof( DefaultEndpointDefinition ) );
            defaultContext.ScopedServices.Should().HaveCount( 1 ).And.Contain( typeof( IActivityMonitor ) );
            defaultContext.SingletonServices.Should().HaveCount( 1 ).And.Contain( typeof( ISomeService ) );

            anotherContext.EndpointDefinition.ClassType.Should().Be( typeof( AnotherEndpointDefinition ) );
            anotherContext.ScopedServices.Should().HaveCount( 1 ).And.Contain( typeof( IActivityMonitor ) );
            anotherContext.SingletonServices
                .Should().HaveCount( 2 )
                .And.Contain( new[] { typeof( ISomeRefinedService ), typeof( ISomeService ) } );
        }

    }
}
