// Ignore Spelling: Deconstruct

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
    public interface IEndpointResult : IStObjEndpointServiceInfo
    {
        /// <summary>
        /// Gets the default context.
        /// </summary>
        IEndpointContext DefaultEndpointContext { get; }

        /// <summary>
        /// Gets all the <see cref="EndpointContext"/>. The first one is the <see cref="DefaultEndpointContext"/>.
        /// </summary>
        IReadOnlyList<IEndpointContext> EndpointContexts { get; }
    }
}
