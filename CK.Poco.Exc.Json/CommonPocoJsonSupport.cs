using CK.Setup;

namespace CK.Core;

/// <summary>
/// Triggers the common implementation of Json import and export code generation.
/// </summary>
/// <remarks>
/// This class should be static but because of the stupid https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs0718
/// it is not so it can be used as a generic argument.
/// </remarks>
[ContextBoundDelegation( "CK.Setup.PocoJson.CommonImpl, CK.Poco.Exc.Json.Engine" )]
[AlsoRegisterType<PocoExchangeService>]
public sealed class CommonPocoJsonSupport
{
}
