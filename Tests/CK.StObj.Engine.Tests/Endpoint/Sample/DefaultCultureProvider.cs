using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public sealed class DefaultCultureProvider : IEndpointUbiquitousServiceDefault<FakeCultureInfo>
    {
        public FakeCultureInfo Default => new FakeCultureInfo( "default" );
    }
}
