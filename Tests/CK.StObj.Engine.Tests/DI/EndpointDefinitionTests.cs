
using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static CK.StObj.Engine.Tests.DI.Conformant.FromTheOutsideIdeaTests;
using static CK.Testing.StObjEngineTestHelper;
// Ignore Spelling: App Backdoor

namespace CK.StObj.Engine.Tests.DI
{
    [TestFixture]
    public class SampleEndpointTests
    {
        public interface ICallerInfo : IScopedAutoService
        {
            string Address { get; }
            string Protocol { get; }
        }


        public class CommandProcessor : IScopedAutoService
        {
            readonly IActivityMonitor _monitor;
            readonly ICallerInfo _callerInfo;

            public CommandProcessor( IActivityMonitor monitor, ICallerInfo info )
            {
                _monitor = monitor;
                _callerInfo = info;
            }

            public void Process( object command )
            {
                _monitor.Info( $"Processed command '{command}' (coming from '{_callerInfo.Protocol}:{_callerInfo.Address}')." );
            }
        }


        [EndpointDefinition]
        public abstract class BackgroundEndpointDefinition : EndpointDefinition
        {
        }

        public class BackgroundEndpoint : ISingletonAutoService
        {
            readonly BackgroundEndpointDefinition _definition;

            public BackgroundEndpoint( BackgroundEndpointDefinition definition )
            {
                Debug.Assert( definition.Name == "Background" );
                _definition = definition;
            }

            public void HandleCommand( object command )
            {

            }

        }


    }

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
            TestHelper.GetFailedResult( c1 );
            var c2 = TestHelper.CreateStObjCollector( typeof( NoWay2Definition ) );
            TestHelper.GetFailedResult( c2 );
        }


        [EndpointDefinition]
        public abstract class BadNameDefinition : EndpointDefinition
        {
        }

        [Test]
        public void EndpointDefinition_type_name_matters()
        {
            const string msg = "Invalid EndpointDefinition type 'EndpointDefinitionTests.BadNameDefinition': "
                               + "EndpointDefinition type name must end with \"EndpointDefinition\" (the prefix becomes the simple endpoint name).";

            var c1 = TestHelper.CreateStObjCollector( typeof( BadNameDefinition ) );
            TestHelper.GetFailedResult( c1 );

            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                var c2 = TestHelper.CreateStObjCollector();
                c2.SetEndpointScopedService( TestHelper.Monitor, typeof( IActivityMonitor ), typeof( BadNameDefinition ) )
                    .Should().BeFalse();
                logs.Should().Contain( msg );
            }
        }

    }
}
