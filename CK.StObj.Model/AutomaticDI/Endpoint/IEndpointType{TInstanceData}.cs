using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Core
{
    /// <summary>
    /// The EndpointType is the link between the <see cref="EndpointDefinition"/> and the
    /// services world. It is available in the global container and provides a dedicated
    /// container for the endpoint.
    /// </summary>
    /// <typeparam name="TInstanceData">Type of the scoped specific instance data.</typeparam>
    public interface IEndpointType<TInstanceData> : IEndpointType
         where TInstanceData : class
    {
        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> for this endpoint definition that
        /// can create configured <see cref="AsyncServiceScope"/> thanks to <see cref="EndpointServiceProvider{TInstanceData}.CreateAsyncScope(TInstanceData)"/>.
        /// </summary>
        /// <returns>The endpoint service provider.</returns>
        EndpointServiceProvider<TInstanceData> GetContainer();
    }

}
