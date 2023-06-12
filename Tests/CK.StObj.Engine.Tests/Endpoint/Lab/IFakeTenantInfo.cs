using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [TEMPEndpointScopedService( isUbiquitousEndpointInfo: true )]
    public interface IFakeTenantInfo : IAutoService
    {
        string Name { get; }
    }
}
