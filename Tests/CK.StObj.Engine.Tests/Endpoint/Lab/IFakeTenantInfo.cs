using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// This is a ubiquitous IAutoService.
    /// Any of its inheritance chain can be registered (no need to register each specialization level)
    /// and a single IEndpointUbiquitousServiceDefault implementation can exist that will resolve the default
    /// value for all the specialization levels.
    /// </summary>
    [EndpointScopedService( isUbiquitousEndpointInfo: true )]
    public interface IFakeTenantInfo : IAutoService
    {
        string Name { get; }
    }
}
