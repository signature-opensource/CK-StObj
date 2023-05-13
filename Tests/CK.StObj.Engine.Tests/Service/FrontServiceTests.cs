using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{

    [TestFixture]
    public class FrontServiceTests
    {


        [EndpointServiceAvailability( typeof(DefaultEndpointType) )]
        public interface IEndpointService1 : IScopedAutoService
        {
        }

        public class EndpointService1 : IEndpointService1
        {
        }

        [Test]
        public void simple_front_only_registration()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.SimpleMappings[typeof( IEndpointService1 )];
            descriptor.Should().BeSameAs( map.Services.SimpleMappings[typeof( EndpointService1 )] );

            descriptor.IsScoped.Should().BeTrue();
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsProcessService | AutoServiceKind.IsScoped );
        }

        [EndpointServiceAvailability( typeof( DefaultEndpointType ) )]
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
                TestHelper.GetFailedResult( collector, "RealObject cannot be a Endpoint or Process service (type is a class)." );
            }
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible1 ) );
                TestHelper.GetFailedResult( collector, "Invalid CK type combination 'IsAutoService|IsSingleton|IsRealObject|IsProcessService': RealObject cannot be a Endpoint or Process service (type is a class)." );
            }
        }

        public class RealObjectAndAutoService : IRealObject, IAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_externally_defined_as_Process_services()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( RealObjectAndAutoService ), AutoServiceKind.IsProcessService );
            collector.RegisterType( TestHelper.Monitor, typeof( RealObjectAndAutoService ) );
            TestHelper.GetFailedResult( collector, "is already registered as a 'IsAutoService|IsSingleton|IsRealObject [IsMarkerInterface]'. It can not be defined as IsProcessService [ProcessService:External]." );
        }

        public class FrontDependentService1 : IAutoService
        {
            public FrontDependentService1( IEndpointService1 f1 )
            {
            }
        }

        [Test]
        public void an_impl_that_depends_on_a_front_service_is_a_Front_service()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( EndpointService1 ), typeof( FrontDependentService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.SimpleMappings[typeof( FrontDependentService1 )];
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
        }

        public interface IFrontDependentService2 : IAutoService
        {
        }

        public class FrontDependentService2 : IFrontDependentService2
        {
            public FrontDependentService2( FrontDependentService1 f1 )
            {
            }
        }

        [Test]
        public void Front_services_are_transitively_propagated_through_the_constructors()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( FrontDependentService2 ), typeof( FrontDependentService1 ), typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor dDep2 = map.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.SimpleMappings[typeof( FrontDependentService1 )];
            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IEndpointService1 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
        }

        public class OneSingleton : ISingletonAutoService
        {
            public OneSingleton( IUnknwon dep ) { }
        }

        public interface IUnknwon : IAutoService { }

        public class Unknwon : IUnknwon
        {
            public Unknwon( Scoped s ) { }
        }

        public class Scoped : IScopedAutoService { }

        public class ServiceFreeLifetime : IAutoService
        {
            public ServiceFreeLifetime( IUnknwon dep ) { }
        }

        [Test]
        public void propagation_through_an_intermediate_service_1()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( Scoped ), typeof( Unknwon ), typeof( OneSingleton ) );
            TestHelper.GetFailedResult( collector, "is marked as IsSingleton but parameter 'dep' of type 'IUnknwon' in constructor is Scoped." );
        }

        [Test]
        public void propagation_through_an_intermediate_service_2()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( Scoped ), typeof( Unknwon ), typeof( ServiceFreeLifetime ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings[typeof( ServiceFreeLifetime )].IsScoped.Should().BeTrue();
        }
    }
}
