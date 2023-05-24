using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Core
{

    /// <summary>
    /// Base class for a endpoint definition.
    /// The specialized class must be decorated with <see cref="EndpointDefinitionAttribute"/>.
    /// </summary>
    [CKTypeDefiner]
    public abstract class EndpointDefinition<TScopeData> : EndpointDefinition
        where TScopeData : notnull
    {
        /// <summary>
        /// Must be implemented to configure the endpoint services.
        /// Resolutions can rely on the scoped <see cref="EndpointScopeData{TScopeData}"/> that is
        /// necessarily available.
        /// </summary>
        /// <param name="services">Container to configure.</param>
        public abstract void ConfigureEndpointServices( IServiceCollection services );
    }

}
