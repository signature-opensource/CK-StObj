using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public sealed class DefaultCultureProvider : IAmbientServiceDefaultProvider<FakeCultureInfo>
    {
        public FakeCultureInfo Default => new FakeCultureInfo( "default" );
    }
}
