using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint;

public sealed class DefaultCultureProvider : IAmbientServiceDefaultProvider<ExternalCultureInfo>
{
    public ExternalCultureInfo Default => new ExternalCultureInfo( "default" );
}
