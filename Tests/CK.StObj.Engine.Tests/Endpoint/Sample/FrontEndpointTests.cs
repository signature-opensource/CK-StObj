using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{

    [TestFixture]
    public class FrontEndpointTests
    {
        [Test]
        public async Task global_DI_automatically_falls_back_to_default_value_provider_for_ubiquitous_info_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( FakeTenantInfo ),
                                                     typeof( DefaultTenantProvider ) );
            using var services = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Request monitor" ) );
                services.Services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            } ).Services;

            await TestHelper.StartHostedServicesAsync( services );
            using( var scoped = services.CreateScope() )
            {
                var tenant = scoped.ServiceProvider.GetService<IFakeTenantInfo>();
                Debug.Assert( tenant != null );
                tenant.Name.Should().Be( "DefaultTenant" );
            }
        }

        [DIContainerDefinition( DIContainerKind.Endpoint )]
        public abstract class SomeFrontDIContainerDefinition : DIContainerDefinition<SomeFrontDIContainerDefinition.Data>
        {
            public sealed class Data : IScopedData
            {
                internal IActivityMonitor _monitor;
                public Data( IActivityMonitor monitor )
                {
                    _monitor = monitor;
                }
            }

            public override void ConfigureEndpointServices( IServiceCollection services, Func<IServiceProvider, Data> scopeData, IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor>( sp => scopeData( sp )._monitor );
                services.AddScoped<IParallelLogger>( sp => scopeData( sp )._monitor.ParallelLogger );
            }
        }

        [Test]
        public async Task Front_endpoint_default_for_Ambient_services_Async()
        {
            var c = TestHelper.CreateStObjCollector( typeof( SomeFrontDIContainerDefinition ),
                                                     typeof( FakeTenantInfo ),
                                                     typeof( DefaultTenantProvider ),
                                                     typeof( FakeCultureInfo ),
                                                     typeof( DefaultCultureProvider ),
                                                     typeof( FakeAuthenticationInfo ),
                                                     typeof( DefaultAuthenticationInfoProvider ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;

            await TestHelper.StartHostedServicesAsync( services );

            var someFront = services.GetRequiredService<IDIContainer<SomeFrontDIContainerDefinition.Data>>();

            using( TestHelper.Monitor.CollectTexts( out var logs ) )
            {
                using( var scoped = someFront.GetContainer().CreateScope( new SomeFrontDIContainerDefinition.Data( TestHelper.Monitor ) ) )
                {
                    var tenantI = scoped.ServiceProvider.GetRequiredService<IFakeTenantInfo>();
                    var tenantC = scoped.ServiceProvider.GetRequiredService<FakeTenantInfo>();
                    var authI = scoped.ServiceProvider.GetRequiredService<IFakeAuthenticationInfo>();
                    var authC = scoped.ServiceProvider.GetRequiredService<FakeAuthenticationInfo>();
                    var culture = scoped.ServiceProvider.GetRequiredService<FakeCultureInfo>();

                    var monitor = scoped.ServiceProvider.GetRequiredService<IActivityMonitor>();
                    monitor.Trace( $"TenantI: {tenantI.Name}, TenantC: {tenantC.Name}, AuthI: {authI.ActorId}, AuthC: {authC.ActorId}, Cult: {culture.Culture}" );
                }
                logs.Should().Contain( "TenantI: DefaultTenant, TenantC: DefaultTenant, AuthI: 0, AuthC: 0, Cult: default" );
            }
        }

        public sealed class NotEnoughDefaultAuthenticationInfoProvider1 : IEndpointUbiquitousServiceDefault<IFakeAuthenticationInfo>
        {
            readonly FakeAuthenticationInfo _anonymous = new FakeAuthenticationInfo( "", 0 );

            public IFakeAuthenticationInfo Default => _anonymous;
        }

        public sealed class NotEnoughDefaultAuthenticationInfoProvider2 : IEndpointUbiquitousServiceDefault<FakeAuthenticationInfo>
        {
            readonly FakeAuthenticationInfo _anonymous = new FakeAuthenticationInfo( "", 0 );

            public FakeAuthenticationInfo Default => _anonymous;
        }


        [Test]
        public void Ambient_services_are_painful_when_they_are_not_AutoService()
        {
            {
                const string msg = "Unable to find an implementation for 'IEndpointUbiquitousServiceDefault<FakeAuthenticationInfo>'. " +
                                   "Type 'FakeAuthenticationInfo' is not a valid Ambient service, all ambient services must have a default value provider.";
                var c = TestHelper.CreateStObjCollector( typeof( FakeAuthenticationInfo ),
                                                         typeof( NotEnoughDefaultAuthenticationInfoProvider1 ) );
                TestHelper.GetFailedResult( c, msg );
            }
            {
                const string msg = "Unable to find an implementation for 'IEndpointUbiquitousServiceDefault<IFakeAuthenticationInfo>'. " +
                                   "Type 'IFakeAuthenticationInfo' is not a valid Ambient service, all ambient services must have a default value provider.";
                var c = TestHelper.CreateStObjCollector( typeof( FakeAuthenticationInfo ),
                                                         typeof( NotEnoughDefaultAuthenticationInfoProvider2 ) );
                TestHelper.GetFailedResult( c, msg );
            }
        }
    }
}
