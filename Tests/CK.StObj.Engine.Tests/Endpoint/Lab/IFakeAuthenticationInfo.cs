
using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// Mimics the CK.Auth.Abstraction.IAuthenticationInfo interface that is just an interface
    /// declared as a EndpointService | IsScoped by StObjCollector.WellKnownServices AND as a
    /// ubiquitous endpoint info.
    /// </summary>
    //[EndpointScopedService( isUbiquitousEndpointInfo: true )]
    public interface IFakeAuthenticationInfo
    {
        sealed class Anon : IFakeAuthenticationInfo
        {
            public string UserName => "";

            public int ActorId => 0;
        }

        public static readonly IFakeAuthenticationInfo Anonymous = new Anon();

        int ActorId { get; }

        string UserName { get; }
    }
}
