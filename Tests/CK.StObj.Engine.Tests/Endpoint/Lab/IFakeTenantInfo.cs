using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// This is a Ambient IAutoService.
/// Any of its inheritance chain can be registered (no need to register each specialization level)
/// and a single IAmbientServiceDefaultProvider implementation can exist that will resolve the default
/// value for all the specialization levels.
/// </summary>
public interface IFakeTenantInfo : IAmbientAutoService
{
    string Name { get; }
}
