
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
    public class EndpointDefinitionTests
    {
        [EndpointDefinition]
        public abstract class AppIdentityEndpointDefinition : EndpointDefinition
        {
        }

        [EndpointDefinition]
        public abstract class BackdoorEndpointDefinition : EndpointDefinition
        {
        }

        [Test]
        public void EndpointTypeManager_exposes_the_EndpointDefinitions()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AppIdentityEndpointDefinition ), typeof( BackdoorEndpointDefinition ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;

            var manager = s.GetRequiredService<EndpointTypeManager>();
            manager.DefaultEndpointDefinition.Should().NotBeNull();
            manager.AllEndpointDefinitions.Should().HaveCount( 3 );
            manager.AllEndpointDefinitions[0].Should().BeSameAs( manager.DefaultEndpointDefinition );
            manager.AllEndpointDefinitions.Skip(1).Should().Contain( e => e is AppIdentityEndpointDefinition )
                                                     .And.Contain( e => e is BackdoorEndpointDefinition );
        }

        [EndpointDefinition]
        public abstract class NoWay1Definition : BackdoorEndpointDefinition
        {
        }

        [EndpointDefinition]
        public abstract class NoWay2Definition : DefaultEndpointDefinition
        {
        }

        [Test]
        public void EndpointDefinitions_cannot_be_specialized()
        {
            var c1 = TestHelper.CreateStObjCollector( typeof( NoWay1Definition ) );
            TestHelper.GetFailedResult( c1 , "NoWay1Definition' cannot specialize another EndpointDefinition." );
            var c2 = TestHelper.CreateStObjCollector( typeof( NoWay2Definition ) );
            TestHelper.GetFailedResult( c2 , "plouf");
        }

    }
}
