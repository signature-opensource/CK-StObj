﻿using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
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
