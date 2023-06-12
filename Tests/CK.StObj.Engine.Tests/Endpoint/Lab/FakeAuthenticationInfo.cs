using System.Text.RegularExpressions;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// This is also (by inheritance) a endpoint ubiquitous info.
    /// And since it's public it must be handled as a possible ubiquitous
    /// value. However, IFakeAuthenticationInfo is NOT a IAutoService: they act as
    /// 2 unrelated services (standard DI behavior).
    /// </summary>
    public sealed class FakeAuthenticationInfo : IFakeAuthenticationInfo
    {
        public FakeAuthenticationInfo( string name, int id )
        {
            UserName = name;
            ActorId = id;
        }
        public int ActorId { get; }
        public string UserName { get; }

        public override string ToString() => $"{UserName} ({ActorId})";

    }
}
