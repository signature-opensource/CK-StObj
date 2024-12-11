using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;

[TestFixture]
public partial class GlobalizationTypeTests
{
    [ExternalName( "GlobalizationTypes" )]
    public interface IAllTypes : IPoco
    {
        FormattedString? PFormattedString { get; set; }
        CodeString PCodeString { get; set; }
        MCString PMCString { get; set; }
        UserMessage? PUserMessage { get; set; }
        SimpleUserMessage? PSimpleUserMessage { get; set; }
        NormalizedCultureInfo PNormalizedCultureInfo { get; set; }
        ExtendedCultureInfo PExtendedCultureInfo { get; set; }
    }

    [Test]
    public async Task all_globalization_types_roundtrip_Async()
    {
        var current = new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.CodeDefault );

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IAllTypes ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var n = auto.Services.GetRequiredService<IPocoFactory<IAllTypes>>().Create();
        n.PFormattedString = FormattedString.Create( $"Hello {nameof( current )}!", NormalizedCultureInfo.CodeDefault );
        n.PCodeString = CodeString.Create( $"Hello {nameof( current )}!", NormalizedCultureInfo.CodeDefault, "Res.Hello", "file", 3712 );
        n.PMCString = MCString.Create( current, $"Hello {nameof( current )}!", "Res.Hello" );
        n.PUserMessage = UserMessage.Info( current, "Hop!", "Res.Hop" );
        n.PSimpleUserMessage = new SimpleUserMessage( UserMessageLevel.Error, "Error", 5 );
        n.PNormalizedCultureInfo = NormalizedCultureInfo.CodeDefault;
        n.PExtendedCultureInfo = ExtendedCultureInfo.EnsureExtendedCultureInfo( "fr, es" );

        var n2 = JsonTestHelper.Roundtrip( directory, n, text: t => TestHelper.Monitor.Info( $"IAllTypes serialization: " + t ) );
        n2.Should().BeEquivalentTo( n );
    }

    public interface IWithUserMessage : IPoco
    {
        UserMessage? UserMessage { get; set; }
        SimpleUserMessage? SimpleUserMessage { get; set; }
        object? OUserMessage { get; set; }
        object? OSimpleUserMessage { get; set; }
    }


    [Test]
    public async Task with_AlwaysExportSimpleUserMessage_Async()
    {
        var current = new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.CodeDefault );

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithUserMessage ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var n = auto.Services.GetRequiredService<IPocoFactory<IWithUserMessage>>().Create();
        n.UserMessage = UserMessage.Info( current, $"Hop {nameof(current)}!", "Res.Hop" ).With( 5 );
        n.SimpleUserMessage = new SimpleUserMessage( UserMessageLevel.Error, "Hop!", 5 );
        n.OUserMessage = n.UserMessage;
        n.OSimpleUserMessage = n.SimpleUserMessage;

        var s1 = n.ToString();
        s1.Should().Be( """{"UserMessage":[4,"Hop current!",5],"SimpleUserMessage":[16,"Hop!",5],"OUserMessage":["UserMessage",[4,"Hop current!",5]],"OSimpleUserMessage":["SimpleUserMessage",[16,"Hop!",5]]}""" );

        var s2 = n.ToString( new PocoJsonExportOptions() { AlwaysExportSimpleUserMessage = true } );
        s2.Should().Be( """pouf""" );
    }
}
