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
        [EndpointScopedService( typeof(DefaultEndpointDefinition) )]
        public interface IEndpointService1 : IScopedAutoService
        {
        }

        public class EndpointService1 : IEndpointService1
        {
        }

        [Test]
        public void Endpoint_service_are_not_registered_as_auto_service()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.Mappings.ContainsKey( typeof( IEndpointService1 ) ).Should().BeFalse();
        }

        [EndpointScopedService( typeof( DefaultEndpointDefinition ) )]
        public class Impossible0 : IRealObject
        {
        }

        public class Impossible1 : IRealObject, IProcessAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_Endpoint_or_Process_services()
        {
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible0 ) );
                TestHelper.GetFailedResult( collector );
            }
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible1 ) );
                TestHelper.GetFailedResult( collector );
            }
        }

        public class EndpointDependentService1 : IAutoService
        {
            public EndpointDependentService1( IEndpointService1 f1 )
            {
            }
        }

        [Test]
        public void Endpoint_services_only_propagate_their_lifetime_1()
        {
            var collector = TestHelper.CreateStObjCollector( typeof(EndpointService1), typeof( EndpointDependentService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.Mappings[typeof( EndpointDependentService1 )];
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
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
        public void Endpoint_services_only_propagate_their_lifetime_2()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointDependentService2 ),
                                                             typeof( EndpointDependentService1 ),
                                                             typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor dDep2 = map.Services.Mappings[typeof( IEndpointDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.Mappings[typeof( EndpointDependentService1 )];
            map.Services.Mappings.ContainsKey( typeof( IEndpointService1 ) ).Should().BeFalse( "A Endpoint service is not an Automatic service." );
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
        }

    }
}
