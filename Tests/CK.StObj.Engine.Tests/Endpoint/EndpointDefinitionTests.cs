
using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;
// Ignore Spelling: App Backdoor

namespace CK.StObj.Engine.Tests.Endpoint
{
    [TestFixture]
    public class EndpointDefinitionTests
    {
        [EndpointDefinition]
        public abstract class AppIdentityEndpointDefinition : EndpointDefinition<AppIdentityEndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

            public override void ConfigureEndpointServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor,ActivityMonitor>();
                services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            }
        }

        [EndpointDefinition]
        public abstract class BackdoorEndpointDefinition : EndpointDefinition<BackdoorEndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

            public override void ConfigureEndpointServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor, ActivityMonitor>();
                services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            }
        }

        [Test]
        public void EndpointTypeManager_exposes_the_EndpointDefinitions()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AppIdentityEndpointDefinition ), typeof( BackdoorEndpointDefinition ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;

            var manager = s.GetRequiredService<EndpointTypeManager>();
            manager.EndpointDefinitions.Should().HaveCount( 2 );
            manager.EndpointDefinitions.Should().Contain( e => e is AppIdentityEndpointDefinition )
                                                .And.Contain( e => e is BackdoorEndpointDefinition );
        }

        [Test]
        public void EndpointTypes_are_available_in_containers_as_well_as_the_IEnumerable_of_IEndpoint()
        {
            var c = TestHelper.CreateStObjCollector( typeof( AppIdentityEndpointDefinition ), typeof( BackdoorEndpointDefinition ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;

            // From the root (singleton) container.
            var o1 = GetEndpointsAndOtherTrueSingletons( s );
            var backdoor = s.GetRequiredService<IEndpointType<BackdoorEndpointDefinition.Data>>();
            var appIdentity = s.GetRequiredService<IEndpointType<AppIdentityEndpointDefinition.Data>>();

            using var sScope = s.CreateScope();
            var o2 = GetEndpointsAndOtherTrueSingletons( sScope.ServiceProvider );

            var sB = backdoor.GetContainer();
            var sA = appIdentity.GetContainer();

            var o3 = GetEndpointsAndOtherTrueSingletons( sB );
            var o4 = GetEndpointsAndOtherTrueSingletons( sA );

            using var sScopeA = sA.CreateScope();
            using var sScopeB = sB.CreateScope();

            var o5 = GetEndpointsAndOtherTrueSingletons( sScopeA.ServiceProvider );
            var o6 = GetEndpointsAndOtherTrueSingletons( sScopeB.ServiceProvider );

            o1.SequenceEqual( o2 ).Should().BeTrue();
            o2.SequenceEqual( o3 ).Should().BeTrue();
            o3.SequenceEqual( o4 ).Should().BeTrue();
            o4.SequenceEqual( o5 ).Should().BeTrue();
            o5.SequenceEqual( o6 ).Should().BeTrue();
        }

        static object[] GetEndpointsAndOtherTrueSingletons( IServiceProvider s )
        {
            var endpoints = s.GetRequiredService<IEnumerable<IEndpointType>>();
            endpoints.Should().HaveCount( 2 );
            var appIdentity = s.GetRequiredService<IEndpointType<AppIdentityEndpointDefinition.Data>>();
            appIdentity.Name.Should().Be( "AppIdentity" );
            var backdoor = s.GetRequiredService<IEndpointType<BackdoorEndpointDefinition.Data>>();
            backdoor.Name.Should().Be( "Backdoor" );
            endpoints.Should().Contain( appIdentity ).And.Contain( backdoor );
            return new object[] { endpoints, appIdentity, backdoor, s.GetRequiredService<EndpointTypeManager>(), s.GetRequiredService<IStObjMap>() }; 
        }

        [EndpointDefinition]
        public abstract class NoWay1Definition : BackdoorEndpointDefinition
        {
        }

        [Test]
        public void EndpointDefinitions_cannot_be_specialized()
        {
            var c1 = TestHelper.CreateStObjCollector( typeof( NoWay1Definition ) );
            TestHelper.GetFailedResult( c1 , "EndpointDefinition type 'EndpointDefinitionTests.NoWay1Definition' must directly specialize "
                                             + "EndpointDefinition<TScopeData> (not 'EndpointDefinitionTests.BackdoorEndpointDefinition')." );

        }

        [EndpointDefinition]
        public abstract class Dup1EndpointDefinition : EndpointDefinition<Dup1EndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }
        }

        [EndpointDefinition]
        public abstract class Dup2EndpointDefinition : EndpointDefinition<Dup1EndpointDefinition.Data>
        {
        }

        [Test]
        public void EndpointDefinitions_cannot_use_the_same_ScopeData_type()
        {
            var c1 = TestHelper.CreateStObjCollector( typeof( Dup1EndpointDefinition ), typeof( Dup2EndpointDefinition ) );
            TestHelper.GetFailedResult( c1, "Endpoint definition ScopeData must be different." );
        }


        [EndpointDefinition]
        public abstract class BadNameDefinition : EndpointDefinition<BadNameDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

        }

        [Test]
        public void EndpointDefinition_type_name_matters()
        {
            const string msg = "Invalid EndpointDefinition type 'EndpointDefinitionTests.BadNameDefinition': "
                               + "EndpointDefinition type name must end with \"EndpointDefinition\" (the prefix becomes the simple endpoint name).";

            var c1 = TestHelper.CreateStObjCollector( typeof( BadNameDefinition ) );
            TestHelper.GetFailedResult( c1, msg );

            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                var c2 = TestHelper.CreateStObjCollector();
                c2.SetEndpointScopedService( TestHelper.Monitor, typeof( ICKBinaryReader ), typeof( BadNameDefinition ) )
                    .Should().BeFalse();
                logs.Should().Contain( msg );
            }
        }

    }
}
