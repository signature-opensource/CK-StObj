using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Non generic base for <see cref="IEndpointType{TScopeData}"/>.
    /// </summary>
    public interface IEndpointType : IServiceProviderIsService
    {
        /// <summary>
        /// Gets the endpoint definition.
        /// </summary>
        EndpointDefinition EndpointDefinition { get; }

        /// <summary>
        /// Gets this endpoint's name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the singletons services that have been configured by the <see cref="EndpointDefinition{TScopeData}.ConfigureEndpointServices(IServiceCollection, IServiceProviderIsService)"/>
        /// method that are specific to this endpoint.
        /// </summary>
        IReadOnlyCollection<Type> SpecificSingletonServices { get; }

        /// <summary>
        /// Gets the scoped services that have been configured by the <see cref="EndpointDefinition{TScopeData}.ConfigureEndpointServices(IServiceCollection, IServiceProviderIsService)"/>
        /// method that are specific to this endpoint.
        /// </summary>
        IReadOnlyCollection<Type> SpecificScopedServices { get; }

        /// <summary>
        /// Gets the type of the scope data type: the generic parameter of <see cref="EndpointDefinition{TScopeData}"/>
        /// and <see cref="EndpointServiceProvider{TScopeData}"/>.
        /// </summary>
        Type ScopeDataType { get; }
    }

}
