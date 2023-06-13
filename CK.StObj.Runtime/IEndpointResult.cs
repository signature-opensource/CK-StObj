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
        /// Gets all the <see cref="EndpointContext"/>. The first one is the <see cref="DefaultEndpointContext"/>.
        /// </summary>
        IReadOnlyList<IEndpointContext> EndpointContexts { get; }

        /// <summary>
        /// Gets whether at least one ubiquitous information service type exists.
        /// </summary>
        bool HasUbiquitousInfoServices { get; }
    }
}
