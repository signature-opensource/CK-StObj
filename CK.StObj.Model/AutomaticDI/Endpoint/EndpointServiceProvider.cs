using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// A endpoint service provider can create configured <see cref="AsyncServiceScope"/> thanks to <see cref="CreateAsyncScope(TScopeData)"/>.
    /// </summary>
    /// <typeparam name="TScopeData">Data specific to the endpoint from which endpoint scoped services can be derived.</typeparam>
    public sealed class EndpointServiceProvider<TScopeData> : IServiceProvider, IServiceProviderIsService, IDisposable, IAsyncDisposable
        where TScopeData : notnull
    {
        readonly ServiceProvider _serviceProvider;
        IServiceProviderIsService? _serviceProviderIsService;

        /// <summary>
        /// Initializes a new <see cref="EndpointServiceProvider{TScopeData}"/>.
        /// The <paramref name="serviceProvider"/> must be configured to resolve a scoped <see cref="EndpointScopeData{TScopeData}"/>.
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
        public AsyncServiceScope CreateAsyncScope( TScopeData scopedData )
        {
            var scope = _serviceProvider.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<EndpointScopeData<TScopeData>>()._data = scopedData;
            return scope;
        }

        /// <summary>
        /// Creates a new <see cref="IServiceScope"/> that can be used to resolve endpoint scoped services.
        /// </summary>
        /// <param name="scopedData">Endpoint instance specific data.</param>
        /// <returns>An <see cref="IServiceScope"/> that can be used to resolve scoped services.</returns>
        public IServiceScope CreateScope( TScopeData scopedData )
        {
            var scope = _serviceProvider.CreateScope();
            scope.ServiceProvider.GetRequiredService<EndpointScopeData<TScopeData>>()._data = scopedData;
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
        /// <returns>True if the specified service is a available, false if it is not.</returns>
        public bool IsService( Type serviceType )
        {
            return (_serviceProviderIsService ??= _serviceProvider.GetRequiredService<IServiceProviderIsService>()).IsService( serviceType );
        }
    }

}
