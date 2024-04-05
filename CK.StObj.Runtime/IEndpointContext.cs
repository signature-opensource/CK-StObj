using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Defines a endpoint.
    /// </summary>
    public interface IEndpointContext
    {
        /// <summary>
        /// Gets the endpoint name (this is the endpoint definition type name without "EndpointDefinition" suffix).
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the Back/Front kind of this endpoint.
        /// </summary>
        EndpointKind Kind { get; }

        /// <summary>
        /// Gets the endpoint definition.
        /// </summary>
        IStObjResult EndpointDefinition { get; }

        /// <summary>
        ///  Gets the instance data type.
        /// </summary>
        Type? ScopeDataType { get; }
    }
}
