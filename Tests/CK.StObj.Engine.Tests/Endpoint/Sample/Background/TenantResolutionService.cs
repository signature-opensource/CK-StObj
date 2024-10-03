using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint;

[ContainerConfiguredScopedService]
public sealed class TenantResolutionService : IScopedAutoService
{
    public IFakeTenantInfo GetTenantFromRequest( /*HttpContext ctx*/ )
    {
        // var tenantId = ctx.Request.QueryString["TenanId"];
        return new FakeTenantInfo( "AcmeCorp" );
    }
}
