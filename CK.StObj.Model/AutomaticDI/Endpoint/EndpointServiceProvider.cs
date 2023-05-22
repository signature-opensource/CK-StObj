using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// A endpoint service provider can create configured <see cref="AsyncServiceScope"/> thanks to <see cref="CreateAsyncScope(TInstanceData)"/>.
    /// </summary>
    /// <typeparam name="TInstanceData">Data specific to the endpoint from which endpoint scoped services can be derived.</typeparam>
    public sealed class EndpointServiceProvider<TInstanceData> : IServiceProvider, IServiceProviderIsService, IDisposable, IAsyncDisposable
        where TInstanceData : class
    {
        readonly ServiceProvider _serviceProvider;
        IServiceProviderIsService? _serviceProviderIsService;

        /// <summary>
        /// Initializes a new <see cref="EndpointServiceProvider{TInstanceData}"/>.
        /// The <paramref name="serviceProvider"/> must be configured to resolve a scoped <see cref="EndpointInstance{TInstanceData}"/>.
        /// This is not intended to be used directly, this supports the endpoint DI infrastructure.
        /// </summary>
        /// <param name="serviceProvider"></param>
        public EndpointServiceProvider( ServiceProvider serviceProvider )
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Creates a new <see cref="AsyncServiceScope"/> that can be used to resolve endpoint scoped services.
        /// </summary>
        /// <param name="scopedData">Endpoint instance specific data.</param>
        /// <returns>An <see cref="AsyncServiceScope"/> that can be used to resolve scoped services.</returns>
        public AsyncServiceScope CreateAsyncScope( TInstanceData scopedData )
        {
            var scope = _serviceProvider.CreateAsyncScope();
            var d = scope.ServiceProvider.GetRequiredService<EndpointInstance<TInstanceData>>();
            d._data = scopedData;
            return scope;
        }

        /// <inheritdoc />
        public object? GetService( Type serviceType ) => _serviceProvider.GetService( serviceType );

        /// <inheritdoc />
        public void Dispose() => _serviceProvider.Dispose();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

        /// <summary>
        /// Implements <see cref="IServiceProviderIsService.IsService(Type)"/>.
        /// </summary>
        /// <param name="serviceType">The service to test.</param>
        /// <returns>true if the specified service is a available, false if it is not.</returns>
        public bool IsService( Type serviceType )
        {
            var p = _serviceProviderIsService ?? _serviceProvider.GetService<IServiceProviderIsService>();
            return p.IsService( serviceType );
        }
    }

}
