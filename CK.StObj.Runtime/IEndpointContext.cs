using System;
using System.Collections.Generic;

namespace CK.Setup
{
    public interface IEndpointContext
    {
        /// <summary>
        /// Gets the endpoint name (this is the endpoint definition type name without "EndpointDefinition" suffix):
        /// "Default" for <see cref="CK.Core.DefaultEndpointDefinition"/>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the endpoint definition.
        /// </summary>
        IStObjResult EndpointDefinition { get; }

        /// <summary>
        ///  Gets the instance data type. This is null for the <see cref="DefaultEndpointDefinition"/>.
        /// </summary>
        Type? ScopeDataType { get; }
    }
}
