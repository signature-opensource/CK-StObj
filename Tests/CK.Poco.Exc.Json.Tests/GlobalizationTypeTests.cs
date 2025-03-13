using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
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
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
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
        n2.ShouldBeEquivalentTo( n );
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
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var someString = "some string";

        var n = auto.Services.GetRequiredService<IPocoFactory<IWithUserMessage>>().Create();
        n.UserMessage = UserMessage.Info( current, $"Hop {someString}!", "Res.Hop" ).With( 5 );
        n.SimpleUserMessage = new SimpleUserMessage( UserMessageLevel.Error, "Hop!", 5 );
        n.OUserMessage = n.UserMessage;
        n.OSimpleUserMessage = n.SimpleUserMessage;

        var s1 = n.ToString();
        s1.ShouldBe( """
            {"UserMessage":[4,5,"Hop some string!","en","Res.Hop","Hop some string!","en",[4,11]],"SimpleUserMessage":[16,"Hop!",5],"OUserMessage":["UserMessage",[4,5,"Hop some string!","en","Res.Hop","Hop some string!","en",[4,11]]],"OSimpleUserMessage":["SimpleUserMessage",[16,"Hop!",5]]}
            """ );
        // Polymorphism considers the AlwaysExportSimpleUserMessage: OUserMessage has SimpleUserMessageType. 
        var s2 = n.ToString( new PocoJsonExportOptions() { UseCamelCase = false, AlwaysExportSimpleUserMessage = true } );
        s2.ShouldBe( """
            {"UserMessage":[4,"Hop some string!",5],"SimpleUserMessage":[16,"Hop!",5],"OUserMessage":["SimpleUserMessage",[4,"Hop some string!",5]],"OSimpleUserMessage":["SimpleUserMessage",[16,"Hop!",5]]}
            """ );
    }

    public interface IWithOnlyUserMessage : IPoco
    {
        UserMessage? UserMessage { get; set; }
    }

    [Test]
    public async Task when_UserMessage_is_registered_then_SimpleUserMessage_is_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithOnlyUserMessage ) );
        // This is done by the PocoTypeSystemBuilder.
        // Both AnyWriter in CK.Poco.Exc.Json.Engine/Export/Writers/AnyWriter.cs and
        // the PocoTypeIncludeVisitor check that with a DebugAssert.
        // The PocoTypeIncludeVisitor includes SimpleUserMessage when visiting UserMessage.
        //
        // What may be missing here is that in the PocoTypeSystem.TypeSet.Excluder, we should
        // exclude UserMessage if SimpleUserMessage is excluded.
        //
        // We also miss tests with PocoTypeSet here.
        //
        await configuration.RunSuccessfullyAsync().ConfigureAwait( false );
    }
}
