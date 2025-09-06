using CK.Setup;

namespace CK.Core;

/// <summary>
/// Triggers the common implementation of Json import and export code generation.
/// </summary>
[ContextBoundDelegation( "CK.Setup.PocoJson.CommonImpl, CK.Poco.Exc.Json.Engine" )]
[AlsoRegisterType<PocoExchangeService>]
public static class CommonPocoJsonSupport
{
}
