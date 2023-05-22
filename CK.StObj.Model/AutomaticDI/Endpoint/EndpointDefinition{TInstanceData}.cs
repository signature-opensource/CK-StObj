using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Core
{
    /// <summary>
    /// Base class for a endpoint definition.
    /// The specialized class must be decorated with <see cref="EndpointDefinitionAttribute"/>.
    /// </summary>
    [CKTypeDefiner]
    public abstract class EndpointDefinition<TInstanceData> : EndpointDefinition
        where TInstanceData : class
    {
        EndpointServiceProvider<TInstanceData>? _services;
        object _lock;

        /// <summary>
        /// Initializes a new <see cref="EndpointDefinition{TInstanceData}"/>.
        /// This is not intended to be used directly.
        /// </summary>
        protected EndpointDefinition()
        {
            _lock = new object();
        }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> for this endpoint definition that
        /// can create configured <see cref="AsyncServiceScope"/> thanks to <see cref="EndpointServiceProvider{TInstanceData}.CreateAsyncScope(TInstanceData)"/>.
        /// </summary>
        /// <returns>The endpoint service provider.</returns>
        public EndpointServiceProvider<TInstanceData> GetContainer() => _services ?? DoCreateContainer();

        EndpointServiceProvider<TInstanceData> DoCreateContainer()
        {
            lock( _lock )
            {
                return _services ?? CreateContainer();
            }
        }

        /// <summary>
        /// Creates this definition's container.
        /// This is automatically generated.
        /// </summary>
        /// <returns>The container.</returns>
        protected abstract EndpointServiceProvider<TInstanceData> CreateContainer();

        /// <summary>
        /// Must be implemented to configure the endpoint services.
        /// A <see cref="EndpointInstance{TInstanceData}"/> must be added as scoped service
        /// (this is not done automatically).
        /// </summary>
        /// <param name="services">A copy of the global DI container configuration without any endpoint services.</param>
        public abstract void ConfigureEndpointServices( IServiceCollection services );
    }

}
