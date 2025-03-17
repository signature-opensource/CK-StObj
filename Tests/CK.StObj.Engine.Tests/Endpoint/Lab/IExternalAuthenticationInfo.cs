namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// This does NOT mimic the CK.Auth.Abstraction.IAuthenticationInfo interface: this is just an external interface
/// that is configured to be an Ambient service.
/// <para>
/// Because this is NOT an IAutoService, you're on your own: each specialization level must be explicitly
/// handled when registering and IAmbientServiceDefaultProvider must exist for each of them.
/// </para>
/// The real IAuthenticationInfo is a IAmbientAutoService (like the <see cref="IFakeTenantInfo"/>.
/// </summary>
public interface IExternalAuthenticationInfo
{
    int ActorId { get; }

    string UserName { get; }
}
