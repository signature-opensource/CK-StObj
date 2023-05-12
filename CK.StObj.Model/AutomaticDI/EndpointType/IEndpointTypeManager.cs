using Microsoft.Extensions.Hosting;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Gives access to all the existing <see cref="EndpointType"/>.
    /// </summary>
    public interface IEndpointTypeManager : ISingletonAutoService
    {
        /// <summary>
        /// Gets the default EndpointType.
        /// </summary>
        DefaultEndpointType DefaultEndpointType { get; }

        /// Gets all the EndpointType including the <see cref="DefaultEndpointType"/> (that is the first one).
        IReadOnlyList<EndpointType> AllEndpointTypes { get; }

        static void JustHereToKeepTheMicrosoftExtensionsHostingAbstractionPackageRef( IHostedService service ) { }
    }

}
