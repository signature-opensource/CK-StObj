using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// Because IFakeTenantInfo is a IAutoService, this default provider is enough to satisfy
/// also the default value of FakeTenantInfo.
/// </summary>
public sealed class DefaultTenantProvider : IAmbientServiceDefaultProvider<IFakeTenantInfo>
{
    public IFakeTenantInfo Default => new FakeTenantInfo( "DefaultTenant" );
}
