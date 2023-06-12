using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Core
{
    /// <summary>
    /// Gives access to all the existing <see cref="EndpointDefinition"/>.
    /// This is a singleton service that is available from all endpoint container.
    /// </summary>
    [Setup.ContextBoundDelegation( "CK.Setup.EndpointTypeManagerImpl, CK.StObj.Engine" )]
    public abstract class EndpointTypeManager : ISingletonAutoService
    {
        protected IServiceProvider? _global;

        /// <summary>
        /// Gets the global service provider.
        /// </summary>
        public IServiceProvider GlobalServiceProvider => _global!;

        /// <summary>
        /// Gets all the EndpointDefinition.
        /// </summary>
        public abstract IReadOnlyList<EndpointDefinition> EndpointDefinitions { get; }

        /// <summary>
        /// Gets all the service types that are declared as endpoint services and their kind.
        /// </summary>
        public abstract IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices { get; }

        /// <summary>
        /// Gets the available <see cref="IEndpointType"/>.
        /// </summary>
        public abstract IReadOnlyList<IEndpointType> EndpointTypes { get; }

    }

}
