using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// The EndpointType is the link between the <see cref="EndpointDefinition"/> and the
    /// services world. It is available in the global container and provides a dedicated
    /// container for the endpoint.
    /// <para>
    /// This is not intended to be supported by user code: implementations are automatically generated.
    /// </para>
    /// </summary>
    /// <typeparam name="TScopeData">Type of the scoped specific instance data.</typeparam>
    public interface IEndpointType<TScopeData> : IEndpointType, ISingletonAutoService
         where TScopeData : EndpointDefinition.ScopedData
    {
        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> for this endpoint definition that
        /// can create configured <see cref="AsyncServiceScope"/> thanks to <see cref="IEndpointServiceProvider{TScopeData}.CreateAsyncScope(TScopeData)"/>.
        /// </summary>
        /// <returns>The endpoint service provider.</returns>
        IEndpointServiceProvider<TScopeData> GetContainer();
    }

}
