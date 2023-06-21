
using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// This does NOT mimic the CK.Auth.Abstraction.IAuthenticationInfo interface: this is just an interface
    /// defined as a ubiquitous endpoint info.
    /// <para>
    /// Because this is NOT an IAutoService, you're on your own: each specialization level must be explicitly
    /// handled when registering and IEndpointUbiquitousServiceDefault must exist for them.
    /// </para>
    /// The real IAuthenticationInfo is a IAutoService (like the <see cref="IFakeTenantInfo"/>.
    /// </summary>
    [EndpointScopedService( isUbiquitousEndpointInfo: true )]
    public interface IFakeAuthenticationInfo
    {
        int ActorId { get; }

        string UserName { get; }
    }
}
