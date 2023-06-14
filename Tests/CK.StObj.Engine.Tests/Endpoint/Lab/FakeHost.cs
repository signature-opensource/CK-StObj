using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    static class FakeHost
    {
        public static void ConfigureGlobal( ServiceCollection global )
        {
            // Here we are working with 3 fake ubiquitous services.
            // None of them have code generation (but they could).
            // These simulate resolution from a request token, query parameter or other mean
            // of deducing these informations for the global context.
            global.AddScoped<FakeAuthenticationInfo>( sp => new FakeAuthenticationInfo( "Bob", 42 ) );
            global.AddScoped<IFakeAuthenticationInfo>( sp => sp.GetRequiredService<FakeAuthenticationInfo>() );

            global.AddScoped<FakeCultureInfo>( sp => new FakeCultureInfo( "fr" ) );

            global.AddScoped<FakeTenantInfo>( sp => new FakeTenantInfo( "MyFavoriteTenant" ) );
            global.AddScoped<IFakeTenantInfo>( sp => sp.GetRequiredService<FakeTenantInfo>() );
        }


        // Mimics the code executed at startup based on the Fake objects.
        public static IEndpointServiceProvider<FakeBackEndpointDefinition.Data>? CreateServiceProvider( IActivityMonitor monitor,
                                                                                                        IServiceCollection globalConfiguration,
                                                                                                        out IServiceProvider? globalServiceProvider )
        {
            // 1 - This is the AddStObjMap work. The StObjMap is from the StObj assembly or it's an embedded map:
            //     anyway, we have an instance.
            GeneratedRootContext stObjMap = new GeneratedRootContext();
            var reg = new StObjContextRoot.ServiceRegister( monitor, globalConfiguration );
            if( !stObjMap.ConfigureServices( reg ) )
            {
                globalServiceProvider = null;
                return null;
            }
            // 2 - Once the global DI container is built, the code generated HostedServiceLifetimeTrigger sets the global
            //     container on THE EndpointTypeManager from its constructor: the HostedServiceLifetimeTrigger
            //     is a regular IHostedService, ISingletonAutoService that takes the global IServiceProvider in its constructor.

            // This is done by the application host.
            globalServiceProvider = globalConfiguration.BuildServiceProvider();

            // HostedServiceLifetimeTrigger constructor.
            var theEPTM = ((EndpointTypeManager_CK)globalServiceProvider.GetRequiredService<EndpointTypeManager>());
            theEPTM.SetGlobalContainer( globalServiceProvider );

            // 3 - From now on, on demand (this is lazily initialized), the endpoints are able to expose their
            //     own DI container.
            var endpointType = globalServiceProvider.GetRequiredService<EndpointTypeManager>()
                                .EndpointTypes
                                .OfType<EndpointType<FakeBackEndpointDefinition.Data>>()
                                .Single();
            return endpointType.GetContainer();
        }

    }
}
