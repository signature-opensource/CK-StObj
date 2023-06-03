using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{

    sealed class FakeEndpointDefinition : EndpointDefinition<FakeEndpointDefinition.Data>
    {
        public sealed class Data
        {
            public Data( IFakeAuthenticationInfo info )
            {
                AuthInfo = info;
            }

            public IFakeAuthenticationInfo AuthInfo { get; }
        }

        public override string Name => "Fake";

        // This method is implemented by the developer of the Endpoint.
        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider,Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped( sp => scopeData(sp).AuthInfo );
        }

    }

    static class FakeHost
    {
        // Mimics the code executed at startup based on the Fake objects.
        public static IEndpointServiceProvider<FakeEndpointDefinition.Data>? CreateServiceProvider( IActivityMonitor monitor,
                                                                                                    IServiceCollection globalConfiguration,
                                                                                                     out IServiceProvider? globalServiceProvider )
        {
            // 1 - This is the AddStObjMap work. The StObjMap is from the StObj assembly or it's an embedded map:
            //     anyway, we have an instance.
            FakeStObjMap stObjMap = new FakeStObjMap();
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
            var theEPTM = ((FakeEndpointTypeManager_CK)globalServiceProvider.GetRequiredService<EndpointTypeManager>());
            theEPTM.SetGlobalContainer( globalServiceProvider );

            // 3 - From now on, on demand (this is lazily initialized), the endpoints are able to expose their
            //     own DI container.
            var endpointType = globalServiceProvider.GetRequiredService<EndpointTypeManager>()
                                .EndpointTypes
                                .OfType<EndpointType<FakeEndpointDefinition.Data>>()
                                .Single();
            return endpointType.GetContainer();
        }

    }
}
