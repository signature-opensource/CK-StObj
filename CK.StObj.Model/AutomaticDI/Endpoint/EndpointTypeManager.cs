using CK.Setup;
using Microsoft.Extensions.Hosting;
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
    [ContextBoundDelegation( "CK.Setup.EndpointTypeManagerImpl, CK.StObj.Engine" )]
    public abstract class EndpointTypeManager : ISingletonAutoService
    {
        protected IServiceProvider? _global;

        /// <summary>
        /// Gets the global service provider.
        /// </summary>
        public IServiceProvider GlobalServiceProvider => _global!;

        /// <summary>
        /// Gets the default EndpointDefinition.
        /// </summary>
        public abstract DefaultEndpointDefinition DefaultEndpointDefinition { get; }

        /// <summary>
        /// Gets all the EndpointDefinition including the <see cref="DefaultEndpointDefinition"/> (that is the first one).
        /// </summary>
        public abstract IReadOnlyList<EndpointDefinition> AllEndpointDefinitions { get; }

        /// <summary>
        /// Gets all the service types that are declared as endpoint services.
        /// </summary>
        public abstract IReadOnlySet<Type> EndpointServices { get; }

        /// <summary>
        /// Gets the available <see cref="IEndpointType"/>. This doesn't contain the default endpoint.
        /// </summary>
        public abstract IReadOnlyList<IEndpointType> EndpointTypes { get; }

        // Do not remove this!
        static void KeepTheMicrosoftExtensionsHostingAbstractionAssemblyRef( IHostedService service ) { }
    }

}
