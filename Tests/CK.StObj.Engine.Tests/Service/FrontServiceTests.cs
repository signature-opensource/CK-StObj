using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    public class FrontServiceTests
    {
        public interface IFrontService1 : IFrontAutoService
        {
        }

        public class FrontService1 : IFrontService1
        {
        }

        [Test]
        public void simple_front_only_registration()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( FrontService1 ), AutoServiceKind.IsScoped );
            collector.RegisterType( TestHelper.Monitor, typeof( FrontService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.SimpleMappings[typeof( IFrontService1 )];
            descriptor.Should().BeSameAs( map.Services.SimpleMappings[typeof( FrontService1 )] );

            descriptor.IsScoped.Should().BeTrue();
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsScoped );
        }

        public class Impossible0 : IRealObject, IFrontAutoService
        {
        }
        public class Impossible1 : IRealObject, IFrontProcessAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_FrontOnly_services()
        {
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible0 ) );
                TestHelper.GetFailedResult( collector, "RealObject cannot have a Scoped lifetime, RealObject cannot be a front service for class '" );
            }
            {
                var collector = TestHelper.CreateStObjCollector( typeof( Impossible1 ) );
                TestHelper.GetFailedResult( collector, "Invalid CK type combination: RealObject cannot be a front service" );
            }
        }

        public class RealObjectAndAutoService : IRealObject, IAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_defined_as_FrontOnly_services()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( RealObjectAndAutoService ), AutoServiceKind.IsFrontProcessService );
            collector.RegisterType( TestHelper.Monitor, typeof( RealObjectAndAutoService ) );
            TestHelper.GetFailedResult( collector, "is already registered as a 'IsAutoService|IsSingleton|IsRealObject [IsMarkerInterface]'. It can not be defined as IsFrontProcessService [FrontType:External]." );
        }

        public class FrontDependentService1 : IAutoService
        {
            public FrontDependentService1( IFrontService1 f1 )
            {
            }
        }

        [Test]
        public void an_impl_that_depends_on_a_front_service_is_a_Front_service()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( FrontService1 ), typeof( FrontDependentService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor descriptor = map.Services.SimpleMappings[typeof( FrontDependentService1 )];
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsScoped );
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
            var collector = TestHelper.CreateStObjCollector( typeof( FrontDependentService2 ), typeof( FrontDependentService1 ), typeof( FrontService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor dDep2 = map.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.SimpleMappings[typeof( FrontDependentService1 )];
            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IFrontService1 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsScoped );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsScoped );
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsScoped );
        }

        public class MService1NoAutoService : Model.IMarshaller<FrontService1>
        {
            public FrontService1 Read( ICKBinaryReader reader, IServiceProvider services ) => null!;

            public void Write( ICKBinaryWriter writer, FrontService1 service ) { }
        }

        [Test]
        public void Marshallers_are_not_AutoServices()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( MService1NoAutoService ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings.ContainsKey( typeof( MService1NoAutoService ) ).Should().BeFalse();
        }

        public class MService1 : MService1NoAutoService, IAutoService 
        {
        }

        [Test]
        public void registering_a_Frontservice_with_its_AutoService_Marshaller_makes_it_marshallable()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( FrontService1 ), typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            IStObjServiceClassDescriptor dI = map.Services.SimpleMappings[typeof( IFrontService1 )];
            IStObjServiceClassDescriptor dC = map.Services.SimpleMappings[typeof( FrontService1 )];
            dI.Should().BeSameAs( dC );
            dI.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsScoped );
        }

        [Test]
        public void registered_AutoService_Marshaller_is_mapped_by_the_class_type_and_its_interfaces()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( FrontService1 ), typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            var dM = map.Services.SimpleMappings[typeof( MService1 )];
            var dMi = map.Services.SimpleMappings[typeof( Model.IMarshaller<FrontService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMi.Should().BeSameAs( dM );
        }

        [Test]
        public void registered_AutoService_Marshaller_handles_only_the_exact_type()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( FrontService1 ), typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IFrontService1 )];
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsScoped );

            var dM = map.Services.SimpleMappings[typeof( MService1 )];
            var dMClass = map.Services.SimpleMappings[typeof( Model.IMarshaller<FrontService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMClass.Should().BeSameAs( dM );
            map.Services.SimpleMappings.ContainsKey( typeof( Model.IMarshaller<IFrontService1> ) )
                .Should().BeFalse( "Marshalling the IService MUST be explicitly supported by the marshaller implementation." );
        }


        [Test]
        public void Marshallable_Front_services_are_no_more_propagated()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( FrontDependentService2 ),
                                                             typeof( FrontDependentService1 ),
                                                             typeof( FrontService1 ),
                                                             typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IFrontService1 )];
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsScoped );

            IStObjServiceClassDescriptor dDep2 = map.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsScoped );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsScoped );
        }


        public interface IAmNotAService { }

        public class NotAutoService : IAmNotAService { }

        public class MarshalAnyway : Model.IMarshaller<IAmNotAService>, IAutoService
        {
            public IAmNotAService Read( ICKBinaryReader reader, IServiceProvider services ) => null!;

            public void Write( ICKBinaryWriter writer, IAmNotAService service ) { }
        }

        [Test]
        public void a_mere_service_does_not_become_AutoService_because_a_Marshaller_exists()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( MarshalAnyway ), typeof( NotAutoService ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings.ContainsKey( typeof( IAmNotAService ) ).Should().BeFalse();
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
