using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{

    [TestFixture]
    public class BasicEndpointServiceTests
    {
        [ContainerConfiguredScopedService]
        public interface IEndpointService1 : IScopedAutoService
        {
        }

        public class EndpointService1 : IEndpointService1
        {
        }

        [Test]
        public void Endpoint_service_can_be_registered_as_auto_service()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.Mappings.ContainsKey( typeof( IEndpointService1 ) ).Should().BeTrue();
        }

        [ContainerConfiguredScopedService]
        public class Impossible0 : IRealObject
        {
        }

        [IsMultiple]
        public interface Impossible1 : IRealObject
        {
        }

        [Test]
        public void real_objects_cannot_be_Endpoint_or_Multiple_services()
        {
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible0 ) );
                TestHelper.GetFailedResult( collector,
                    "RealObject cannot have a Scoped lifetime, RealObject cannot be an optional Endpoint service" );
            }
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible1 ) );
                TestHelper.GetFailedResult( collector , "IRealObject interface cannot be marked as a Multiple service" );
            }
        }

        public class EndpointDependentService1 : IAutoService
        {
            public EndpointDependentService1( IEndpointService1 f1 )
            {
            }
        }

        [Test]
        public void currently_Endpoint_services_only_propagate_their_lifetime_1()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointService1 ), typeof( EndpointDependentService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.Mappings[typeof( EndpointDependentService1 )];
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
        }

        public interface IEndpointDependentService2 : IAutoService
        {
        }

        public class EndpointDependentService2 : IEndpointDependentService2
        {
            public EndpointDependentService2( EndpointDependentService1 f1 )
            {
            }
        }

        [Test]
        public void currently_Endpoint_services_only_propagate_their_lifetime_2()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointDependentService2 ),
                                                             typeof( EndpointDependentService1 ),
                                                             typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor dDep2 = map.Services.Mappings[typeof( IEndpointDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.Mappings[typeof( EndpointDependentService1 )];
            map.Services.Mappings.ContainsKey( typeof( IEndpointService1 ) ).Should().BeTrue( "A Endpoint service can be an Automatic service." );
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
        }

    }
}
