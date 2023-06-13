using CK.Core;
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
        /// Gets all the <see cref="EndpointContext"/>. The first one is the <see cref="DefaultEndpointContext"/>.
        /// </summary>
        IReadOnlyList<IEndpointContext> EndpointContexts { get; }

        /// <summary>
        /// Gets all the endpoint service types and their kind (they are not necessarily <see cref="IAutoService"/>).
        /// </summary>
        IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices { get; }

        /// <summary>
        /// Gets whether at least one ubiquitous information service type exists.
        /// </summary>
        bool HasUbiquitousInfoServices { get; }

        /// <summary>
        /// Lists all the ubiquitous service types where <see cref="IAutoService"/> inheritance chains
        /// are expanded. See <see cref="UbiquitousMapping"/>. Order matters: consecutive entries with
        /// the same <see cref="UbiquitousMapping.MappingIndex"/> belong to the same auto service inheritance
        /// chain.
        /// </summary>
        IReadOnlyList<EndpointTypeManager.UbiquitousMapping> UbiquitousMappings { get; }

        /// <summary>
        /// Gets the <see cref="IEndpointUbiquitousServiceDefault{T}"/> to use for each mapped ubiquitous
        /// service.
        /// </summary>
        IReadOnlyList<IStObjFinalClass> DefaultUbiquitousValueProviders { get; }
    }
}
