using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using static CK.StObj.Engine.Tests.DI.EndpointServiceExtensionTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.DI
{
    public class ViolatingTrueSingletonRuleTests
    {
        [EndpointSingletonService( typeof( DefaultEndpointDefinition ) )]
        public interface ISomeService { }

        [EndpointSingletonService( typeof( AnotherEndpointDefinition ) )]
        public interface ISomeRefinedService : ISomeService { }

        // Test below simulates this attribute: thanks to this, the 2 contexts expose their own
        // ISomeService and ISomeRefinedService singleton instance.
        //
        // [assembly: EndpointSingletonService( typeof( ISomeService ), typeof( AnotherEndpointDefinition ), null )]

        [Test]
        public void singletons_service_can_be_different_instances()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AnotherEndpointDefinition ),
                                                     typeof( ISomeRefinedService ),
                                                     typeof( ISomeService ) );
            c.SetEndpointSingletonService( TestHelper.Monitor,
                                           typeof( ISomeService ),
                                           typeof( AnotherEndpointDefinition ),
                                           null );

            var r = TestHelper.GetSuccessfulResult( c ).EndpointResult;
            Debug.Assert( r != null );

            r.EndpointServices.Should().Contain( new[] { typeof( ISomeService ), typeof( ISomeRefinedService ) } );
            r.EndpointContexts.Should().HaveCount( 2 );
            var defaultContext = r.EndpointContexts[0];
            var anotherContext = r.EndpointContexts[1];

            defaultContext.EndpointDefinition.ClassType.Should().Be( typeof( DefaultEndpointDefinition ) );
            defaultContext.ScopedServices.Should().BeEmpty();
            defaultContext.SingletonServices.Should().HaveCount( 1 ).And.Contain( (typeof( ISomeService ), null) );

            anotherContext.EndpointDefinition.ClassType.Should().Be( typeof( AnotherEndpointDefinition ) );
            anotherContext.ScopedServices.Should().BeEmpty();
            anotherContext.SingletonServices
                .Should().HaveCount( 2 )
                .And.Contain( new (Type, IEndpointContext?)[] { (typeof( ISomeRefinedService ), null), (typeof( ISomeService ), null) } );
        }

        // Test below simulates this attribute.
        //
        // This "splits" the singleton: in Another endpoint, ISomeService will be the singleton in Default
        // and ISomeRefinedService will be an independent instance managed by Another.
        // 
        // [assembly: EndpointSingletonServiceType( typeof( ISomeService ), typeof( AnotherEndpointDefinition ), typeof( DefaultEndpointDefinition ) )]

        [Test]
        public void service_can_be_split_in_two_parts()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AnotherEndpointDefinition ),
                                                     typeof( ISomeRefinedService ),
                                                     typeof( ISomeService ) );
            c.SetEndpointSingletonService( TestHelper.Monitor,
                                           typeof( ISomeService ),
                                           typeof( AnotherEndpointDefinition ),
                                           typeof( DefaultEndpointDefinition ) );

            var r = TestHelper.GetSuccessfulResult( c ).EndpointResult;
            Debug.Assert( r != null );

            r.EndpointServices.Should().Contain( new[] { typeof( ISomeService ), typeof( ISomeRefinedService ) } );
            r.EndpointContexts.Should().HaveCount( 2 );
            var defaultContext = r.EndpointContexts[0];
            var anotherContext = r.EndpointContexts[1];

            defaultContext.EndpointDefinition.ClassType.Should().Be( typeof( DefaultEndpointDefinition ) );
            defaultContext.ScopedServices.Should().BeEmpty();
            defaultContext.SingletonServices.Should().HaveCount( 1 ).And.Contain( (typeof( ISomeService ), null) );

            anotherContext.EndpointDefinition.ClassType.Should().Be( typeof( AnotherEndpointDefinition ) );
            anotherContext.ScopedServices.Should().BeEmpty();
            anotherContext.SingletonServices
                .Should().HaveCount( 2 )
                .And.Contain( new (Type, IEndpointContext?)[] { (typeof( ISomeRefinedService ), null), (typeof( ISomeService ), defaultContext) } );
        }



    }
}
