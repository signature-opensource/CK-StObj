using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// Non generic and multiple base for <see cref="IDIContainer{TScopeData}"/>.
/// </summary>
[IsMultiple]
public interface IDIContainer : IServiceProviderIsService
{
    /// <summary>
    /// Gets the endpoint definition.
    /// </summary>
    DIContainerDefinition DIContainerDefinition { get; }

    /// <summary>
    /// Gets this endpoint's name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the singletons services that have been configured by the <see cref="DIContainerDefinition{TScopeData}.ConfigureContainerServices(IServiceCollection, Func{IServiceProvider, TScopeData}, IServiceProviderIsService)"/>
    /// method that are specific to this container.
    /// </summary>
    IReadOnlyCollection<Type> SpecificSingletonServices { get; }

    /// <summary>
    /// Gets the scoped services that have been configured by the <see cref="DIContainerDefinition{TScopeData}.ConfigureContainerServices(IServiceCollection, Func{IServiceProvider, TScopeData}, IServiceProviderIsService)"/>
    /// method that are specific to this container.
    /// </summary>
    IReadOnlyCollection<Type> SpecificScopedServices { get; }

    /// <summary>
    /// Gets the type of the scope data that is the generic argument of <see cref="DIContainerDefinition{TScopeData}"/>
    /// and <see cref="IDIContainerServiceProvider{TScopeData}"/>.
    /// </summary>
    Type ScopeDataType { get; }
}
