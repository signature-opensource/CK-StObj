using CK.Core;
using CK.Setup;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [EndpointScopedService( isUbiquitousEndpointInfo: true )]
    public sealed class FakeCultureInfo
    {
        public FakeCultureInfo( string culture )
        {
            Culture = culture;
        }

        public string Culture { get; }

        public override string ToString() => Culture;
    }
}
