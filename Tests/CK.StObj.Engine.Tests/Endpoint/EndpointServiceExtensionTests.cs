using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint;

[TestFixture]
public class EndpointServiceExtensionTests
{
    [ScopedContainerConfiguredService]
    public interface IEPService1
    {
    }

    [SingletonContainerConfiguredService]
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

    public class AmbientThing
    {
        public string? ThingName { get; set; }
    }

    public sealed class DefaultAmbientThingProvider : IAmbientServiceDefaultProvider<AmbientThing>
    {
        public AmbientThing Default => new AmbientThing() { ThingName = "I'm the default thing name!" };
    }


    [Test]
    public async Task Ambient_service_requires_its_default_value_provider_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( AmbientThing ), ConfigurableAutoServiceKind.IsAmbientService|ConfigurableAutoServiceKind.IsContainerConfiguredService|ConfigurableAutoServiceKind.IsScoped );
        await configuration.GetFailedAutomaticServicesAsync( "Type 'AmbientThing' is not a valid Ambient service, all ambient services must have a default value provider." );

        configuration.FirstBinPath.Types.Add( typeof( DefaultAmbientThingProvider ) );
        await configuration.RunSuccessfullyAsync();
    }

    public class SpecAmbientThing : AmbientThing
    {
    }

    public sealed class SpecAmbientThingProvider : IAmbientServiceDefaultProvider<SpecAmbientThing>
    {
        public SpecAmbientThing Default => new SpecAmbientThing() { ThingName = "I'm the default (spec) thing name!" };
    }

    [Test]
    public async Task specialized_Ambient_service_not_AutoService_cannot_share_the_SpecDefaultProvider_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( AmbientThing ), ConfigurableAutoServiceKind.IsAmbientService|ConfigurableAutoServiceKind.IsContainerConfiguredService|ConfigurableAutoServiceKind.IsScoped );
        configuration.FirstBinPath.Types.Add( [typeof( SpecAmbientThing ), typeof( SpecAmbientThingProvider )] );

        await configuration.GetFailedAutomaticServicesAsync(
            "Unable to find an implementation for 'IAmbientServiceDefaultProvider<EndpointServiceExtensionTests.AmbientThing>'. "
            + "Type 'AmbientThing' is not a valid Ambient service, all ambient services must have a default value provider." );
    }

    public class AutoAmbientThing : IAmbientAutoService
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
    public async Task specialized_Ambient_services_that_are_AutoServices_can_share_the_SpecDefaultProvider_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( [typeof( SpecAutoAmbientThing ),
                                               typeof( AutoAmbientThing ),
                                               typeof( SpecAutoAmbientThingProvider )] );
        await configuration.RunSuccessfullyAsync();
    }


}
