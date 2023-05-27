using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class ProcessServiceMarshallerTests
    {
        public interface IProcessService1 : IProcessAutoService
        {
        }

        public class ProcessService1 : IProcessService1
        {
        }

        public class MService1NoAutoService : Model.IMarshaller<ProcessService1>
        {
            public ProcessService1 Read( ICKBinaryReader reader, IServiceProvider services ) => null!;

            public void Write( ICKBinaryWriter writer, ProcessService1 service ) { }
        }

        [Test]
        public void Marshallers_are_not_AutoServices()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( MService1NoAutoService ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.Mappings.ContainsKey( typeof( MService1NoAutoService ) ).Should().BeFalse();
        }

        public class MService1 : MService1NoAutoService, IAutoService
        {
        }

        // A ProcessService can be singleton or scoped.
        [Test]
        public void registering_a_Processservice_with_its_AutoService_Marshaller_makes_it_marshallable()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ProcessService1 ), typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            IStObjServiceClassDescriptor dI = map.Services.Mappings[typeof( IProcessService1 )];
            IStObjServiceClassDescriptor dC = map.Services.Mappings[typeof( ProcessService1 )];
            dI.Should().BeSameAs( dC );
            dI.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );
        }

        [Test]
        public void registered_AutoService_Marshaller_is_mapped_by_the_class_type_and_its_interfaces()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ProcessService1 ), typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            var dM = map.Services.Mappings[typeof( MService1 )];
            var dMi = map.Services.Mappings[typeof( Model.IMarshaller<ProcessService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMi.Should().BeSameAs( dM );
        }

        [Test]
        public void registered_AutoService_Marshaller_handles_only_the_exact_type()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ProcessService1 ), typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor d1 = map.Services.Mappings[typeof( IProcessService1 )];
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );

            var dM = map.Services.Mappings[typeof( MService1 )];
            var dMClass = map.Services.Mappings[typeof( Model.IMarshaller<ProcessService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMClass.Should().BeSameAs( dM );
            map.Services.Mappings.ContainsKey( typeof( Model.IMarshaller<IProcessService1> ) )
                .Should().BeFalse( "Marshalling the IService MUST be explicitly supported by the marshaller implementation." );
        }

        public class ProcessDependentService1 : IAutoService
        {
            public ProcessDependentService1( IProcessService1 f1 )
            {
            }
        }
        public interface IProcessDependentService2 : IAutoService
        {
        }

        public class ProcessDependentService2 : IProcessDependentService2
        {
            public ProcessDependentService2( ProcessDependentService1 f1 )
            {
            }
        }


        [Test]
        public void Marshallable_Front_services_are_no_more_propagated()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ProcessDependentService2 ),
                                                             typeof( ProcessDependentService1 ),
                                                             typeof( ProcessService1 ),
                                                             typeof( MService1 ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjServiceClassDescriptor d1 = map.Services.Mappings[typeof( IProcessService1 )];
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );

            IStObjServiceClassDescriptor dDep2 = map.Services.Mappings[typeof( IProcessDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = map.Services.Mappings[typeof( IProcessDependentService2 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsProcessService | AutoServiceKind.IsMarshallable | AutoServiceKind.IsSingleton );
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
            map.Services.Mappings.ContainsKey( typeof( IAmNotAService ) ).Should().BeFalse();
        }


        public class RealObjectAndAutoService : IRealObject, IAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_externally_defined_as_Process_services()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( RealObjectAndAutoService ), AutoServiceKind.IsProcessService );
            collector.RegisterType( typeof( RealObjectAndAutoService ) );
            TestHelper.GetFailedResult( collector );
        }

    }
}
