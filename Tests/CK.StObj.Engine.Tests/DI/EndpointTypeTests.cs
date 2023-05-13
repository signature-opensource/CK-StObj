
using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;
// Ignore Spelling: App Backdoor

namespace CK.StObj.Engine.Tests.DI
{
    [TestFixture]
    public class EndpointTypeTests
    {

        public class AppIdentityEndpointType : EndpointType
        {
        }

        public class BackdoorEndpointType : EndpointType
        {
        }


        [Test]
        public void EndpointTypeManager_exposes_the_EndpointTypes()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AppIdentityEndpointType ), typeof( BackdoorEndpointType ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;

            var manager = s.GetRequiredService<EndpointTypeManager>();
            manager.DefaultEndpointType.Should().NotBeNull();
            manager.AllEndpointTypes.Should().HaveCount( 3 );
            manager.AllEndpointTypes[0].Should().BeSameAs( manager.DefaultEndpointType );
            manager.AllEndpointTypes.Skip(1).Should().Contain( e => e is AppIdentityEndpointType )
                                                     .And.Contain( e => e is BackdoorEndpointType );
        }
    }
}
