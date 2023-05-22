using CK.Setup;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Gives access to all the existing <see cref="EndpointDefinition"/>.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.EndpointTypeManagerImpl, CK.StObj.Engine" )]
    public abstract class EndpointTypeManager : ISingletonAutoService
    {
        /// <summary>
        /// Gets the default EndpointDefinition.
        /// </summary>
        public abstract DefaultEndpointDefinition DefaultEndpointDefinition { get; }

        /// <summary>
        /// Gets all the EndpointDefinition including the <see cref="DefaultEndpointDefinition"/> (that is the first one).
        /// </summary>
        public abstract IReadOnlyList<EndpointDefinition> AllEndpointDefinitions { get; }

        // Do not remove this!
        static void KeepTheMicrosoftExtensionsHostingAbstractionAssemblyRef( IHostedService service ) { }
    }

}
