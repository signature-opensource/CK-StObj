using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Base class for a endpoint definition.
    /// The specialized class must be decorated with <see cref="EndpointDefinitionAttribute"/>.
    /// The <typeparamref name="TScopeData"/> is a key as well as the <see cref="EndpointDefinition.Name"/>: all endpoint
    /// must have different name and different scope data type otherwise a setup error will occur.
    /// </summary>
    /// <typeparam name="TScopeData">Type of the scoped data that is injected in <see cref="IEndpointServiceProvider{TScopeData}"/>.</typeparam>
    [CKTypeDefiner]
    public abstract class EndpointDefinition<TScopeData> : EndpointDefinition
        where TScopeData : EndpointDefinition.ScopedData
    {
        /// <summary>
        /// Must be implemented to configure the endpoint services.
        /// Resolutions can rely on <paramref name="scopeData"/>, for instance:
        /// <code>
        /// services.AddScoped&lt;IMySpecificService&gt;( sp => scopeData( sp ).MySpecificService );
        /// </code>
        /// Note that if the endpoint doesn't get a scoped IActivityMonitor from the scoped data, the right registration is:
        /// <code>
        /// services.AddScoped&lt;IActivityMonitor,ActivityMonitor&gt;();
        /// services.AddScoped( sp => sp.GetRequiredService&lt;IActivityMonitor&gt;().ParallelLogger );
        /// </code>
        /// If the monitor is provided, this becomes:
        /// <code>
        /// services.AddScoped&lt;IActivityMonitor&gt;( sp => scopeData( sp ).Monitor );
        /// services.AddScoped( sp => sp.GetRequiredService&lt;IActivityMonitor&gt;().ParallelLogger );
        /// </code>
        /// </summary>
        /// <param name="services">Container to configure.</param>
        /// <param name="globalServiceExists">Provides a way to detect if a service is available.</param>
        public abstract void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider,TScopeData> scopeData,     
                                                        IServiceProviderIsService globalServiceExists );

        /// <summary>
        /// Infrastructure artifact not intended to be called directly.
        /// This is called before calling <see cref="ConfigureEndpointServices(IServiceCollection, Func{IServiceProvider, TScopeData}, IServiceProviderIsService)"/>
        /// to register the ubiquitous endpoint services.
        /// </summary>
        /// <param name="services">The services to configure.</param>
        /// <param name="scopeData">The scoped data accessor.</param>
        public abstract void ConfigureUbiquitousEndpointInfoServices( IServiceCollection services, Func<IServiceProvider, TScopeData> scopeData );

    }

}
