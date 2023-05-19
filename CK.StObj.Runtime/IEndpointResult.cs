using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Captures the information about endpoint services: this is a reverse index of the
    /// attributes declaration.
    /// </summary>
    public interface IEndpointResult
    {
        /// <summary>
        /// Gets the default context.
        /// </summary>
        IEndpointContext DefaultEndpointContext { get; }

        /// <summary>
        /// Gets all the <see cref="EndpointContext"/>. The first one is the <see cref="DefaultEndpointContext"/>.
        /// </summary>
        IReadOnlyList<IEndpointContext> EndpointContexts { get; }

        /// <summary>
        /// Gets all the endpoint service types.
        /// </summary>
        IEnumerable<Type> EndpointServices { get; }

        /// <summary>
        /// Gets whether a type is a endpoint service.
        /// </summary>
        /// <param name="type">The type to test.</param>
        /// <returns>True if the type is a endpoint service, false otherwise.</returns>
        bool IsEndpointService( Type type );
    }
}
