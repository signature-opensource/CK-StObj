using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class FrontProcessServiceMarshallerTests
    {
        public interface IFrontProcessService1 : IFrontProcessAutoService
        {
        }

        public class FrontProcessService1 : IFrontProcessService1
        {
        }

        public class MService1NoAutoService : Model.IMarshaller<FrontProcessService1>
        {
            public FrontProcessService1 Read( ICKBinaryReader reader, IServiceProvider services ) => null!;

            public void Write( ICKBinaryWriter writer, FrontProcessService1 service ) { }
        }

        [Test]
        public void Marshallers_are_not_AutoServices()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( MService1NoAutoService ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings.ContainsKey( typeof( MService1NoAutoService ) ).Should().BeFalse();
        }

        public class MService1 : MService1NoAutoService, IAutoService
        {
        }

        // A FrontProcessService can be singleton or scoped.
        [Test]
        public void registering_a_Frontservice_with_its_AutoService_Marshaller_makes_it_marshallable()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontProcessService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            IStObjServiceClassDescriptor dI = map.Services.SimpleMappings[typeof( IFrontProcessService1 )];
            IStObjServiceClassDescriptor dC = map.Services.SimpleMappings[typeof( FrontProcessService1 )];
            dI.Should().BeSameAs( dC );
            dI.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );
        }

        [Test]
        public void registered_AutoService_Marshaller_is_mapped_by_the_class_type_and_its_interfaces()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontProcessService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            var dM = map.Services.SimpleMappings[typeof( MService1 )];
            var dMi = map.Services.SimpleMappings[typeof( Model.IMarshaller<FrontProcessService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMi.Should().BeSameAs( dM );
        }

        [Test]
        public void registered_AutoService_Marshaller_handles_only_the_exact_type()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontProcessService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IFrontProcessService1 )];
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );

            var dM = map.Services.SimpleMappings[typeof( MService1 )];
            var dMClass = map.Services.SimpleMappings[typeof( Model.IMarshaller<FrontProcessService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMClass.Should().BeSameAs( dM );
            map.Services.SimpleMappings.ContainsKey( typeof( Model.IMarshaller<IFrontProcessService1> ) )
                .Should().BeFalse( "Marshalling the IService MUST be explicitly supported by the marshaller implementation." );
        }

        public class FrontProcessDependentService1 : IAutoService
        {
            public FrontProcessDependentService1( IFrontProcessService1 f1 )
            {
            }
        }

        public interface IFrontProcessDependentService2 : IAutoService
        {
        }

        public class FrontProcessDependentService2 : IFrontProcessDependentService2
        {
            public FrontProcessDependentService2( FrontProcessDependentService1 f1 )
            {
            }
        }


        [Test]
        public void Marshallable_Front_services_are_no_more_propagated()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontProcessDependentService2 ) );
            collector.RegisterType( typeof( FrontProcessDependentService1 ) );
            collector.RegisterType( typeof( FrontProcessService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor d1 = map.Services.SimpleMappings[typeof( IFrontProcessService1 )];
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );

            IStObjServiceClassDescriptor dDep2 = map.Services.SimpleMappings[typeof( IFrontProcessDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.SimpleMappings[typeof( IFrontProcessDependentService2 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( MarshalAnyway ) );
            collector.RegisterType( typeof( NotAutoService ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings.ContainsKey( typeof( IAmNotAService ) ).Should().BeFalse();
        }
    }
}