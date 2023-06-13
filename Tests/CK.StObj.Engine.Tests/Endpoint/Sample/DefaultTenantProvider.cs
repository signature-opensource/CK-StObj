using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public sealed class DefaultTenantProvider : IEndpointUbiquitousServiceDefault<IFakeTenantInfo>
    {
        public IFakeTenantInfo Default => new FakeTenantInfo( "DefaultTenant" );
    }
}
