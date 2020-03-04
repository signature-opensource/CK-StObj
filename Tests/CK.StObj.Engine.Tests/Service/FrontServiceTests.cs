using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
            collector.DefineAsExternalScoped( new[] { typeof( FrontService1 ) } );
            collector.RegisterType( typeof( FrontService1 ) );

            var result = TestHelper.GetSuccessfulResult( collector );

            IStObjServiceClassDescriptor descriptor = result.Services.SimpleMappings[typeof( IFrontService1 )];
            descriptor.Should().BeSameAs( result.Services.SimpleMappings[typeof( FrontService1 )] );

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
            collector.DefineAsExternalFrontService( new[] { typeof( RealObjectAndAutoService ) }, AutoServiceKind.IsFrontProcessService );
            collector.RegisterType( typeof( RealObjectAndAutoService ) );
            TestHelper.GetFailedResult( collector );
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontService1 ) );
            collector.RegisterType( typeof( FrontDependentService1 ) );

            var r = TestHelper.GetSuccessfulResult( collector );
            IStObjServiceClassDescriptor descriptor = r.Services.SimpleMappings[typeof( FrontDependentService1 )];
            descriptor.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsSingleton );
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
            collector.RegisterType( typeof( FrontService1 ) );

            var r = TestHelper.GetSuccessfulResult( collector );
            IStObjServiceClassDescriptor dDep2 = r.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = r.Services.SimpleMappings[typeof( FrontDependentService1 )];
            IStObjServiceClassDescriptor d1 = r.Services.SimpleMappings[typeof( IFrontService1 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsSingleton );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsSingleton );
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsSingleton );
        }

        public class MService1NoAutoService : Model.IMarshaller<FrontService1>
        {
            public FrontService1 Read( ICKBinaryReader reader ) => null;

            public void Write( ICKBinaryWriter writer, FrontService1 service ) { }
        }

        [Test]
        public void Marshallers_are_not_AutoServices()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( MService1NoAutoService ) );

            var r = TestHelper.GetSuccessfulResult( collector );
            r.Services.SimpleMappings.ContainsKey( typeof( MService1NoAutoService ) ).Should().BeFalse();
        }

        public class MService1 : MService1NoAutoService, IAutoService 
        {
        }

        [Test]
        public void registering_a_Frontservice_with_its_AutoService_Marshaller_makes_it_marshallable()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var r = TestHelper.GetSuccessfulResult( collector );
            IStObjServiceClassDescriptor dI = r.Services.SimpleMappings[typeof( IFrontService1 )];
            IStObjServiceClassDescriptor dC = r.Services.SimpleMappings[typeof( FrontService1 )];
            dI.Should().BeSameAs( dC );
            dI.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallableService | AutoServiceKind.IsSingleton );
        }

        [Test]
        public void registered_AutoService_Marshaller_is_mapped_by_the_class_type_and_its_interfaces()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var r = TestHelper.GetSuccessfulResult( collector );

            var dM = r.Services.SimpleMappings[typeof( MService1 )];
            var dMi = r.Services.SimpleMappings[typeof( Model.IMarshaller<FrontService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMi.Should().BeSameAs( dM );
        }

        [Test]
        public void registered_AutoService_Marshaller_handles_only_the_exact_type()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var r = TestHelper.GetSuccessfulResult( collector );

            var dM = r.Services.SimpleMappings[typeof( MService1 )];
            var dMClass = r.Services.SimpleMappings[typeof( Model.IMarshaller<FrontService1> )];
            dM.Should().NotBeNull();
            dM.IsScoped.Should().BeFalse( "Nothing prevents the marshaller to be singleton." );
            dM.AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton, "A marshaller is not a Front service." );
            dMClass.Should().BeSameAs( dM );
            r.Services.SimpleMappings.ContainsKey( typeof( Model.IMarshaller<IFrontService1> ) )
                .Should().BeFalse( "Mashalling the IService MUST be explicitly supported by the marshaller implementation." );
        }


        [Test]
        public void Marshallable_Front_services_are_no_more_propagated()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontDependentService2 ) );
            collector.RegisterType( typeof( FrontDependentService1 ) );
            collector.RegisterType( typeof( FrontService1 ) );
            collector.RegisterType( typeof( MService1 ) );

            var r = TestHelper.GetSuccessfulResult( collector );
            IStObjServiceClassDescriptor dDep2 = r.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor dDep1 = r.Services.SimpleMappings[typeof( IFrontDependentService2 )];
            IStObjServiceClassDescriptor d1 = r.Services.SimpleMappings[typeof( IFrontService1 )];
            dDep2.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallableService | AutoServiceKind.IsSingleton );
            dDep1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallableService | AutoServiceKind.IsSingleton );
            d1.AutoServiceKind.Should().Be( AutoServiceKind.IsFrontProcessService | AutoServiceKind.IsFrontService | AutoServiceKind.IsMarshallableService | AutoServiceKind.IsSingleton );
        }

    }
}
