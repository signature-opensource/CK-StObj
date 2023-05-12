using Microsoft.Extensions.Hosting;

namespace CK.Core
{
    /// <summary>
    /// The default endpoint type models the global dependency container
    /// built by the application host.
    /// It can be associated to services that cannot or must not be available
    /// in other endpoints.
    /// </summary>
    public sealed class DefaultEndpointType : EndpointType
    {
    }

}
