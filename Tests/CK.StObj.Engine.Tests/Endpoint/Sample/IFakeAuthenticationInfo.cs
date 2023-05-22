
namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// Mimics the CK.Auth.Abstraction.IAuthenticationInfo interface that is just an interface
    /// declared as a EndpointService | IsScoped by StObjCollector.WenllKnownServices.
    /// </summary>
    public interface IFakeAuthenticationInfo
    {
        int ActorId { get; }
        string UserName { get; }
    }
}
