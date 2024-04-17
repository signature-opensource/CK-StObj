using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// IFakeAuthenticationInfo / FakeAuthenticationInfo are NOT auto services.
    /// Default provider must exist for both of them.
    /// But nothing prevents to implement the 2 defaults on the same service!
    /// </summary>
    public sealed class DefaultAuthenticationInfoProvider : IEndpointUbiquitousServiceDefault<IFakeAuthenticationInfo>,
                                                            IEndpointUbiquitousServiceDefault<FakeAuthenticationInfo>
    {
        readonly FakeAuthenticationInfo _anonymous = new FakeAuthenticationInfo( "", 0 );

        FakeAuthenticationInfo IEndpointUbiquitousServiceDefault<FakeAuthenticationInfo>.Default => _anonymous;

        IFakeAuthenticationInfo IEndpointUbiquitousServiceDefault<IFakeAuthenticationInfo>.Default => _anonymous;
    }
}
