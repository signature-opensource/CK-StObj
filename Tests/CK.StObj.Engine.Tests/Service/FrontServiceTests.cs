using CK.Core;
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

        public class FrontDependentService1
        {
            public FrontDependentService1( IFrontService1 f1 )
            {
            }
        }

        [Test]
        public void an_impl_that_depends_on_a_front_service_a_Front_service()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( FrontService1 ) );
            collector.RegisterType( typeof( FrontDependentService1 ) );

            TestHelper.GetSuccessfulResult( collector );
        }
    }
}
