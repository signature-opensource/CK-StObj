using CK.Setup;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Gives access to all the existing <see cref="EndpointType"/>.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.EndpointTypeManagerImpl, CK.StObj.Engine" )]
    public abstract class EndpointTypeManager : ISingletonAutoService
    {
        /// <summary>
        /// Gets the default EndpointType.
        /// </summary>
        public abstract DefaultEndpointType DefaultEndpointType { get; }

        /// <summary>
        /// Gets all the EndpointType including the <see cref="DefaultEndpointType"/> (that is the first one).
        /// </summary>
        public abstract IReadOnlyList<EndpointType> AllEndpointTypes { get; }

        static void KeepTheMicrosoftExtensionsHostingAbstractionAssemblyRef( IHostedService service ) { }
    }

}
