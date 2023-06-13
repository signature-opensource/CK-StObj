using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public sealed class DefaultAuthenticationInfoProvider : IEndpointUbiquitousServiceDefault<IFakeAuthenticationInfo>
    {
        public IFakeAuthenticationInfo Default => new FakeAuthenticationInfo( "", 0 );
    }
}
