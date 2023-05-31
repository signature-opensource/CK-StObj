using Microsoft.Extensions.DependencyInjection;
using System;

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
        where TScopeData : notnull
    {
        /// <summary>
        /// Must be implemented to configure the endpoint services.
        /// Resolutions can rely on <paramref name="scopeData"/>, for instance:
        /// <code>
        /// services.AddScoped&lt;IMySpecificService&gt;( sp => scopeData( sp ).MySpecificService );
        /// </code>
        /// </summary>
        /// <param name="services">Container to configure.</param>
        /// <param name="globalServiceExists">Provides a way to detect if a service is available.</param>
        public abstract void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider,TScopeData> scopeData,     
                                                        IServiceProviderIsService globalServiceExists );
    }

}
