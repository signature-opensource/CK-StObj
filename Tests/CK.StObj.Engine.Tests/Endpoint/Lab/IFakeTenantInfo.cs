using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    //[EndpointScopedService( isUbiquitousEndpointInfo: true )]
    public interface IFakeTenantInfo : IAutoService
    {
        string Name { get; }
    }
}
