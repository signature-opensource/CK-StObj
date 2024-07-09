using CK.Core;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [TestFixture]
    public class EndpointServiceExtensionTests
    {
        [ContainerConfiguredScopedService]
        public interface IEPService1
        {
        }

        [ContainerConfiguredSingletonService]
        public interface IEPService2 : IAutoService
        {
        }

        [Test]
        public void endpoint_services_are_registered_whether_they_are_IAutoService_or_not()
        {
            var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IEPService1 ), typeof( IEPService2 )] ).EndpointResult;
            Debug.Assert( r != null );
            r.Containers.Should().HaveCount( 0 );
            r.EndpointServices[typeof( IEPService1 )].Should().Be( AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsScoped );
            r.EndpointServices[typeof( IEPService2 )].Should().Be( AutoServiceKind.IsContainerConfiguredService | AutoServiceKind.IsSingleton | AutoServiceKind.IsAutoService );
        }

        [ContainerConfiguredScopedService( isAmbientService: true )]
        public class AmbientThing
        {
            public string? ThingName { get; set; }
        }

        public sealed class DefaultAmbientThingProvider : IAmbientServiceDefaultProvider<AmbientThing>
        {
            public AmbientThing Default => new AmbientThing() { ThingName = "I'm the default thing name!" };
        }


        [Test]
        public void Ambient_service_requires_its_default_value_provider()
        {
            TestHelper.GetFailedCollectorResult( [typeof( AmbientThing )], "Type 'AmbientThing' is not a valid Ambient service, all ambient services must have a default value provider." );

            TestHelper.GetSuccessfulCollectorResult( [typeof( AmbientThing ), typeof( DefaultAmbientThingProvider )] );
        }

        public class SpecAmbientThing : AmbientThing
        {
        }

        public sealed class SpecAmbientThingProvider : IAmbientServiceDefaultProvider<SpecAmbientThing>
        {
            public SpecAmbientThing Default => new SpecAmbientThing() { ThingName = "I'm the default (spec) thing name!" };
        }

        [Test]
        public void specialized_Ambient_service_not_AutoService_cannot_share_the_SpecDefaultProvider()
        {
            TestHelper.GetFailedCollectorResult( [typeof( SpecAmbientThing ), typeof( SpecAmbientThingProvider )],
                "Unable to find an implementation for 'IAmbientServiceDefaultProvider<EndpointServiceExtensionTests.AmbientThing>'. "
                + "Type 'AmbientThing' is not a valid Ambient service, all ambient services must have a default value provider." );
        }

        [ContainerConfiguredScopedService( isAmbientService: true )]
        public class AutoAmbientThing : IAutoService
        {
            readonly string _name;

            public string ThingName => _name;

            // This tests that no public constructor is allowed on Endpoint AutoService.
            protected AutoAmbientThing( string name )
            {
                _name = name;
            }
        }

        public class SpecAutoAmbientThing : AutoAmbientThing
        {
            protected SpecAutoAmbientThing( string name ) : base( name ) { }

            public static SpecAutoAmbientThing Create( string name ) => new SpecAutoAmbientThing( name );
        }

        public sealed class SpecAutoAmbientThingProvider : IAmbientServiceDefaultProvider<SpecAutoAmbientThing>
        {
            public SpecAutoAmbientThing Default
            {
                get
                {

                    var s = SpecAutoAmbientThing.Create( "I'm the default (AutoService spec) thing name!" );
                    return s;
                }
            }
        }

        [Test]
        public void specialized_Ambient_services_that_are_AutoServices_can_share_the_SpecDefaultProvider()
        {
            TestHelper.GetSuccessfulCollectorResult( [typeof( SpecAutoAmbientThing ),
                                                      typeof( AutoAmbientThing ),
                                                      typeof( SpecAutoAmbientThingProvider )] );
        }


    }
}
