using System;

namespace CK.Core
{
    /// <summary>
    /// Non generic base for <see cref="IEndpointType{TInstanceData}"/>.
    /// </summary>
    [IsMultiple]
    public interface IEndpointType
    {
        /// <summary>
        /// Gets the endpoint definition.
        /// </summary>
        EndpointDefinition EndpointDefinition { get; }

        /// <summary>
        /// Gets the type of the scoped instance data for this endpoint.
        /// </summary>
        Type InstanceDataType { get; }
    }

}
