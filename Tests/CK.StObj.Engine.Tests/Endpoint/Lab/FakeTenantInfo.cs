namespace CK.StObj.Engine.Tests.Endpoint
{
    public class FakeTenantInfo : IFakeTenantInfo
    {
        public FakeTenantInfo( string name ) => Name = name;

        public string Name { get; }
    }
}
