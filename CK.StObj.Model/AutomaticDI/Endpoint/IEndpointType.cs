using System;

namespace CK.Core
{
    /// <summary>
    /// Non generic base for <see cref="IEndpointType{TScopeData}"/>.
    /// </summary>
    public interface IEndpointType
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
        /// Gets the type of the scope data type: the generic parameter of <see cref="EndpointDefinition{TScopeData}"/>
        /// and <see cref="EndpointServiceProvider{TScopeData}"/>.
        /// </summary>
        Type ScopeDataType { get; }
    }

}
