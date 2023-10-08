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

        public FakeCultureInfo( string culture, string fallbackCulture )
        {
            Culture = culture;
            FallbackCulture = fallbackCulture;
        }

        public string Culture { get; }

        public string? FallbackCulture { get; }

        public override string ToString() => Culture;
    }
}
