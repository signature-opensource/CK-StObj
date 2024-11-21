using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace CK.Core;

/// <summary>
/// A container service provider can create configured <see cref="AsyncServiceScope"/> or <see cref="IServiceScope"/>
/// thanks to <see cref="CreateAsyncScope(TScopeData)"/> or <see cref="CreateScope(TScopeData)"/>.
/// </summary>
/// <typeparam name="TScopeData">Data specific to the endpoint from which endpoint scoped services can be derived.</typeparam>
public interface IDIContainerServiceProvider<TScopeData> : IServiceProvider, IServiceProviderIsService, IDisposable, IAsyncDisposable
    where TScopeData : notnull
{
    /// <summary>
    /// Creates a new <see cref="AsyncServiceScope"/> that can be used to resolve endpoint scoped services.
    /// </summary>
    /// <param name="scopedData">Endpoint instance specific data.</param>
    /// <returns>An <see cref="AsyncServiceScope"/> that can be used to resolve scoped services.</returns>
    AsyncServiceScope CreateAsyncScope( TScopeData scopedData );

    /// <summary>
    /// Creates a new <see cref="IServiceScope"/> that can be used to resolve endpoint scoped services.
    /// </summary>
    /// <param name="scopedData">Endpoint instance specific data.</param>
    /// <returns>An <see cref="IServiceScope"/> that can be used to resolve scoped services.</returns>
    IServiceScope CreateScope( TScopeData scopedData );
}
