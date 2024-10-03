using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// IFakeAuthenticationInfo / FakeAuthenticationInfo are NOT auto services.
/// Default provider must exist for both of them.
/// But nothing prevents to implement the 2 defaults on the same service!
/// </summary>
public sealed class DefaultAuthenticationInfoProvider : IAmbientServiceDefaultProvider<IFakeAuthenticationInfo>,
                                                        IAmbientServiceDefaultProvider<FakeAuthenticationInfo>
{
    readonly FakeAuthenticationInfo _anonymous = new FakeAuthenticationInfo( "", 0 );

    FakeAuthenticationInfo IAmbientServiceDefaultProvider<FakeAuthenticationInfo>.Default => _anonymous;

    IFakeAuthenticationInfo IAmbientServiceDefaultProvider<IFakeAuthenticationInfo>.Default => _anonymous;
}
