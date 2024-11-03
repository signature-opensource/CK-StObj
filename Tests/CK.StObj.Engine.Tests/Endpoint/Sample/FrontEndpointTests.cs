using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint;


[TestFixture]
public class FrontEndpointTests
{
    [Test]
    public async Task global_DI_automatically_falls_back_to_default_value_provider_for_Ambient_services_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.AddRangeArray( typeof( FakeTenantInfo ),
                                                        typeof( DefaultTenantProvider ) );

        using var auto = (await configuration.RunAsync()).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped<IActivityMonitor>( sp => new ActivityMonitor( "Request monitor" ) );
            services.AddScoped<IParallelLogger>( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
        } );

        using( var scoped = auto.Services.CreateScope() )
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

        public override void ConfigureContainerServices( IServiceCollection services, Func<IServiceProvider, Data> scopeData, IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped<IActivityMonitor>( sp => scopeData( sp )._monitor );
            services.AddScoped<IParallelLogger>( sp => scopeData( sp )._monitor.ParallelLogger );
        }
    }

    [Test]
    public async Task Front_endpoint_default_for_Ambient_services_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IExternalAuthenticationInfo ), Setup.ConfigurableAutoServiceKind.IsAmbientService|Setup.ConfigurableAutoServiceKind.IsContainerConfiguredService|Setup.ConfigurableAutoServiceKind.IsScoped );
        configuration.FirstBinPath.Types.Add( typeof( ExternalCultureInfo ), Setup.ConfigurableAutoServiceKind.IsAmbientService | Setup.ConfigurableAutoServiceKind.IsContainerConfiguredService | Setup.ConfigurableAutoServiceKind.IsScoped );
        configuration.FirstBinPath.Types.Add( typeof( SomeFrontDIContainerDefinition ),
                                              typeof( FakeTenantInfo ),
                                              typeof( DefaultTenantProvider ),
                                              typeof( DefaultCultureProvider ),
                                              typeof( ExternalAuthenticationInfo ),
                                              typeof( DefaultAuthenticationInfoProvider ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        // No services configuration here: the IAmbientServiceDefaultProvider<T> must provide
        // the defaults.
        var someFront = auto.Services.GetRequiredService<IDIContainer<SomeFrontDIContainerDefinition.Data>>();

        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            using( var scoped = someFront.GetContainer().CreateScope( new SomeFrontDIContainerDefinition.Data( TestHelper.Monitor ) ) )
            {
                var tenantI = scoped.ServiceProvider.GetRequiredService<IFakeTenantInfo>();
                var tenantC = scoped.ServiceProvider.GetRequiredService<FakeTenantInfo>();
                var authI = scoped.ServiceProvider.GetRequiredService<IExternalAuthenticationInfo>();
                var authC = scoped.ServiceProvider.GetRequiredService<ExternalAuthenticationInfo>();
                var culture = scoped.ServiceProvider.GetRequiredService<ExternalCultureInfo>();

                var monitor = scoped.ServiceProvider.GetRequiredService<IActivityMonitor>();
                monitor.Trace( $"TenantI: {tenantI.Name}, TenantC: {tenantC.Name}, AuthI: {authI.ActorId}, AuthC: {authC.ActorId}, Cult: {culture.Culture}" );
            }
            logs.Should().Contain( "TenantI: DefaultTenant, TenantC: DefaultTenant, AuthI: 0, AuthC: 0, Cult: default" );
        }
    }

    public sealed class NotEnoughDefaultAuthenticationInfoProvider1 : IAmbientServiceDefaultProvider<IExternalAuthenticationInfo>
    {
        readonly ExternalAuthenticationInfo _anonymous = new ExternalAuthenticationInfo( "", 0 );

        public IExternalAuthenticationInfo Default => _anonymous;
    }

    public sealed class NotEnoughDefaultAuthenticationInfoProvider2 : IAmbientServiceDefaultProvider<ExternalAuthenticationInfo>
    {
        readonly ExternalAuthenticationInfo _anonymous = new ExternalAuthenticationInfo( "", 0 );

        public ExternalAuthenticationInfo Default => _anonymous;
    }


    [Test]
    public async Task Ambient_services_are_painful_when_they_are_not_AutoService_Async()
    {
        {
            const string msg = "Unable to find an implementation for 'IAmbientServiceDefaultProvider<ExternalAuthenticationInfo>'. " +
                               "Type 'ExternalAuthenticationInfo' is not a valid Ambient service, all ambient services must have a default value provider.";

            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IExternalAuthenticationInfo ), Setup.ConfigurableAutoServiceKind.IsAmbientService | Setup.ConfigurableAutoServiceKind.IsContainerConfiguredService | Setup.ConfigurableAutoServiceKind.IsScoped );
            configuration.FirstBinPath.Types.Add( [typeof( ExternalAuthenticationInfo ), typeof( NotEnoughDefaultAuthenticationInfoProvider1 )] );

            await configuration.GetFailedAutomaticServicesAsync( msg );
        }
        {
            const string msg = "Unable to find an implementation for 'IAmbientServiceDefaultProvider<IExternalAuthenticationInfo>'. " +
                               "Type 'IExternalAuthenticationInfo' is not a valid Ambient service, all ambient services must have a default value provider.";

            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IExternalAuthenticationInfo ), Setup.ConfigurableAutoServiceKind.IsAmbientService | Setup.ConfigurableAutoServiceKind.IsContainerConfiguredService | Setup.ConfigurableAutoServiceKind.IsScoped );
            configuration.FirstBinPath.Types.Add( [typeof( ExternalAuthenticationInfo ), typeof( NotEnoughDefaultAuthenticationInfoProvider2 )] );

            await configuration.GetFailedAutomaticServicesAsync( msg );
        }
    }
}
