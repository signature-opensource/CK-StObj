using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Reflection;
using static CK.StObj.Engine.Tests.DI.EndpointServiceExtensionTests;
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

    public class ViolatingTrueSingletonRuleTests
    {
        [EndpointSingletonServiceOwner( typeof( DefaultEndpointDefinition ), exclusive: false )]
        public interface ISomeService { }

        [EndpointSingletonServiceOwner( typeof( AnotherEndpointDefinition ), exclusive: true )]
        public interface ISomeRefinedService : ISomeService { }

        // Test below simulates this attribute.
        //
        // This "splits" the singleton: in Another endpoint, ISomeService will be the singleton in Default
        // and ISomeRefinedService will be an independent instance managed by Another.
        // 
        // [assembly: EndpointAvailableServiceType( typeof( ISomeService ), typeof( AnotherEndpointDefinition ) )]

        [Test]
        public void service_can_be_split_in_two_parts()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AnotherEndpointDefinition ),
                                                     typeof( ISomeRefinedService ),
                                                     typeof( ISomeService ) );
            c.SetEndpointAvailableService( TestHelper.Monitor, typeof( ISomeService ), typeof( AnotherEndpointDefinition ) );

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
                .And.Contain( new (Type, EndpointContext?)[] { (typeof( ISomeRefinedService ), null), (typeof( ISomeService ), defaultContext) } );
        }


        // Test below simulates this attribute: thanks to this, the 2 contexts expose their own
        // ISomeService and ISomeRefinedService singleton instance.
        //
        // [assembly: EndpointSingletonOwnerServiceType( typeof( ISomeService ), typeof( AnotherEndpointDefinition ) )]

        [Test]
        public void singletons_service_can_be_different_instances()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AnotherEndpointDefinition ),
                                                     typeof( ISomeRefinedService ),
                                                     typeof( ISomeService ) );
            c.SetEndpointSingletonServiceOwner( TestHelper.Monitor, typeof( ISomeService ), typeof( AnotherEndpointDefinition ), exclusive: false );

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
                .And.Contain( new (Type, EndpointContext?)[] { (typeof( ISomeRefinedService ), null), (typeof( ISomeService ), defaultContext) } );
        }


    }
}
