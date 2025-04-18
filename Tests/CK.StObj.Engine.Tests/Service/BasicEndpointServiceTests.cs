using CK.Core;
using CK.Testing;
using Shouldly;
using NUnit.Framework;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;


[TestFixture]
public class BasicEndpointServiceTests
{
    [ScopedContainerConfiguredService]
    public interface IEndpointService1 : IScopedAutoService
    {
    }

    public class EndpointService1 : IEndpointService1
    {
    }

    [Test]
    public void Endpoint_service_can_be_registered_as_auto_service()
    {
        var map = TestHelper.GetSuccessfulCollectorResult( [typeof( EndpointService1 )] ).EngineMap;
        Throw.DebugAssert( map != null );

        map.Services.Mappings.ContainsKey( typeof( IEndpointService1 ) ).ShouldBeTrue();
    }

    [ScopedContainerConfiguredService]
    public class Impossible0 : IRealObject
    {
    }

    [IsMultiple]
    public interface Impossible1 : IRealObject
    {
    }

    [Test]
    public async Task real_objects_cannot_be_Endpoint_or_Multiple_services_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( Impossible0 ) );

            await configuration.GetFailedAutomaticServicesAsync(
                "RealObject cannot have a Scoped lifetime, RealObject cannot be an optional Endpoint service" );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( Impossible1 ) );
            await configuration.GetFailedAutomaticServicesAsync( "IRealObject interface cannot be marked as a Multiple service" );
        }
    }

    public class EndpointDependentService1 : IAutoService
    {
        public EndpointDependentService1( IEndpointService1 f1 )
        {
        }
    }

    [Test]
    public void currently_Endpoint_services_only_propagate_their_lifetime_1()
    {
        var map = TestHelper.GetSuccessfulCollectorResult( [typeof( EndpointService1 ), typeof( EndpointDependentService1 )] ).EngineMap;
        Debug.Assert( map != null, "No initialization error." );

        IStObjServiceClassDescriptor descriptor = map.Services.Mappings[typeof( EndpointDependentService1 )];
        descriptor.AutoServiceKind.ShouldBe( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
    }

    public interface IEndpointDependentService2 : IAutoService
    {
    }

    public class EndpointDependentService2 : IEndpointDependentService2
    {
        public EndpointDependentService2( EndpointDependentService1 f1 )
        {
        }
    }

    [Test]
    public void currently_Endpoint_services_only_propagate_their_lifetime_2()
    {
        var map = TestHelper.GetSuccessfulCollectorResult( [typeof( EndpointDependentService2 ), typeof( EndpointDependentService1 ), typeof( EndpointService1 )] ).EngineMap;
        Debug.Assert( map != null, "No initialization error." );

        IStObjServiceClassDescriptor dDep2 = map.Services.Mappings[typeof( IEndpointDependentService2 )];
        IStObjServiceClassDescriptor dDep1 = map.Services.Mappings[typeof( EndpointDependentService1 )];
        map.Services.Mappings.ContainsKey( typeof( IEndpointService1 ) ).ShouldBeTrue( "A Endpoint service can be an Automatic service." );
        dDep2.AutoServiceKind.ShouldBe( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
        dDep1.AutoServiceKind.ShouldBe( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
    }

}
