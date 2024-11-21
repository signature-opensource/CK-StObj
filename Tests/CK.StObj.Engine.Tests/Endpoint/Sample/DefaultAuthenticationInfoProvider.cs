using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// IFakeAuthenticationInfo / FakeAuthenticationInfo are NOT auto services.
/// Default provider must exist for both of them.
/// But nothing prevents to implement the 2 defaults on the same service!
/// </summary>
public sealed class DefaultAuthenticationInfoProvider : IAmbientServiceDefaultProvider<IExternalAuthenticationInfo>,
                                                        IAmbientServiceDefaultProvider<ExternalAuthenticationInfo>
{
    readonly ExternalAuthenticationInfo _anonymous = new ExternalAuthenticationInfo( "", 0 );

    ExternalAuthenticationInfo IAmbientServiceDefaultProvider<ExternalAuthenticationInfo>.Default => _anonymous;

    IExternalAuthenticationInfo IAmbientServiceDefaultProvider<IExternalAuthenticationInfo>.Default => _anonymous;
}
