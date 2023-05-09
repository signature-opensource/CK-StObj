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
        public interface IEndpointService1 : IEndpointAutoService
        {
        }

        public class EndpointService1 : IEndpointService1
        {
        }

        [Test]
        public void simple_front_only_registration()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( EndpointService1 ), AutoServiceKind.IsScoped );
            collector.RegisterType( typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.SimpleMappings[typeof( IEndpointService1 )];
            descriptor.Should().BeSameAs( map.Services.SimpleMappings[typeof( EndpointService1 )] );

            descriptor.IsScoped.Should().BeTrue();
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsEndpointService | AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsScoped );
        }

        public class Impossible0 : IRealObject, IEndpointAutoService
        {
        }
        public class Impossible1 : IRealObject, IFrontProcessAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_FrontOnly_services()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( Impossible0 ) );
                TestHelper.GetFailedResult( collector );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( Impossible1 ) );
                TestHelper.GetFailedResult( collector );
            }
        }

        public class RealObjectAndAutoService : IRealObject, IAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_defined_as_FrontOnly_services()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( RealObjectAndAutoService ), AutoServiceKind.IsFrontProcessService );
            collector.RegisterType( typeof( RealObjectAndAutoService ) );
            TestHelper.GetFailedResult( collector );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( EndpointService1 ) );
            collector.RegisterType( typeof( FrontDependentService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.SimpleMappings[typeof( FrontDependentService1 )];
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontDependentService2 ) );
            collector.RegisterType( typeof( FrontDependentService1 ) );
            collector.RegisterType( typeof( EndpointService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor dDep2 = map.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.SimpleMappings[typeof( FrontDependentService1 )];
            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IEndpointService1 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( Scoped ) );
            collector.RegisterType( typeof( Unknwon ) );
            collector.RegisterType( typeof( OneSingleton ) );

            TestHelper.GetFailedResult( collector );
        }

        [Test]
        public void propagation_through_an_intermediate_service_2()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( Scoped ) );
            collector.RegisterType( typeof( Unknwon ) );
            collector.RegisterType( typeof( ServiceFreeLifetime ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings[typeof( ServiceFreeLifetime )].IsScoped.Should().BeTrue();
        }
    }
}
