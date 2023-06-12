using CK.Core;
using Microsoft.Extensions.DependencyInjection;
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
        /// Gets all the endpoint service types and their kind (they are not necessarily <see cref="IAutoService"/>).
        /// </summary>
        IReadOnlyDictionary<Type,AutoServiceKind> EndpointServices { get; }

        /// <summary>
        /// Gets the ubiquitous information service types.
        /// </summary>
        IReadOnlyList<Type> UbiquitousInfoServices { get; }
    }
}
