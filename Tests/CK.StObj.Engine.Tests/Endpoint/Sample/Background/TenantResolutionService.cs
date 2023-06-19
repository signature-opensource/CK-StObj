using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [EndpointScopedService]
    public sealed class TenantResolutionService : IScopedAutoService
    {
        public IFakeTenantInfo GetTenantFromRequest( /*HttpContext ctx*/ )
        {
            // var tenantId = ctx.Request.QueryString["TenanId"];
            return new FakeTenantInfo( "AcmeCorp" );
        }
    }
}
