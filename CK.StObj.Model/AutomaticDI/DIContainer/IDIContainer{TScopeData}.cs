using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Core;

/// <summary>
/// The DIContainer is the link between the <see cref="DIContainerDefinition"/> and the
/// services world. It is available in the global container and provides a dedicated
/// service provider for the container.
/// <para>
/// This is not intended to be supported by user code: implementations are automatically generated.
/// </para>
/// </summary>
/// <typeparam name="TScopeData">Type of the scoped specific instance data.</typeparam>
public interface IDIContainer<TScopeData> : IDIContainer, ISingletonAutoService
     where TScopeData : class, DIContainerDefinition.IScopedData
{
    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> for this container definition that
    /// can create configured <see cref="AsyncServiceScope"/> thanks to <see cref="IDIContainerServiceProvider{TScopeData}.CreateAsyncScope(TScopeData)"/>
    /// (and <see cref="IServiceScope"/> thanks to <see cref="IDIContainerServiceProvider{TScopeData}.CreateScope(TScopeData)"/>).
    /// </summary>
    /// <returns>The container service provider.</returns>
    IDIContainerServiceProvider<TScopeData> GetContainer();
}
