namespace CK.StObj.Engine.Tests.Endpoint;

/// <summary>
/// External class that is defined by configuration to be an Ambient service.
/// </summary>
public sealed class ExternalCultureInfo
{
    public ExternalCultureInfo( string culture )
    {
        Culture = culture;
    }

    public ExternalCultureInfo( string culture, string fallbackCulture )
    {
        Culture = culture;
        FallbackCulture = fallbackCulture;
    }

    public string Culture { get; }

    public string? FallbackCulture { get; }

    public override string ToString() => Culture;
}
