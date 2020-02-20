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
            descriptor.IsFrontOnly.Should().BeTrue();
        }

        public class Impossible : IRealObject,  IFrontAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_FrontOnly_services()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( Impossible ) );
            TestHelper.GetFailedResult( collector );
        }

        public class RealObjectAndAutoService : IRealObject, IAutoService
        {
        }

        [Test]
        public void real_objects_cannot_be_declared_as_FrontOnly_services()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.DefineAsExternalFrontOnly( new[] { typeof( RealObjectAndAutoService ) } );
            collector.RegisterType( typeof( RealObjectAndAutoService ) );
            TestHelper.GetFailedResult( collector );
        }

        //public class FrontDependentService1 : IAutoService
        //{
        //    public FrontDependentService1( IFrontService1 f1 )
        //    {
        //    }
        //}

        //[Test]
        //public void an_impl_that_depends_on_a_front_service_is_a_Front_service()
        //{
        //    var collector = TestHelper.CreateStObjCollector();
        //    collector.RegisterType( typeof( FrontService1 ) );
        //    collector.RegisterType( typeof( FrontDependentService1 ) );

        //    TestHelper.GetSuccessfulResult( collector );
        //}
    }
}
