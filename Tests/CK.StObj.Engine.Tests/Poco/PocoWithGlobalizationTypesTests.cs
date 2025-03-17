using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class PocoWithGlobalizationTypesTests
{
    public interface IWithGlobalization : IPoco
    {
        // Default must be NormalizedCultureInfo.CodeDefault.
        ExtendedCultureInfo ExtendedCultureInfo { get; set; }
        // Default must be NormalizedCultureInfo.CodeDefault.
        NormalizedCultureInfo NormalizedCultureInfo { get; set; }

        // Default of SimpleUserMessage, UserMessage and FormattedString are not valid.
        // We forbid the default for them: they can't be Poco fields.
        //
        // UserMessage UserMessage { get; set; }
        // SimpleUserMessage SimpleUserMessage { get; set; }
        // FormattedString FormattedString { get; set; }

        // Default must be MCString.Empty.
        MCString MCString { get; set; }
        // Default must be CodeString.Empty.
        CodeString CodeString { get; set; }

        ExtendedCultureInfo? NExtendedCultureInfo { get; set; }
        NormalizedCultureInfo? NNormalizedCultureInfo { get; set; }
        SimpleUserMessage? NSimpleUserMessage { get; set; }
        UserMessage? NUserMessage { get; set; }
        MCString? NMCString { get; set; }
        CodeString? NCodeString { get; set; }
        FormattedString? NFormattedString { get; set; }
    }

    [Test]
    public async Task default_for_Globalization_types_are_handled_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IWithGlobalization ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var p = auto.Services.GetRequiredService<IPocoFactory<IWithGlobalization>>().Create();

        p.ExtendedCultureInfo.ShouldBeSameAs( NormalizedCultureInfo.CodeDefault );
        p.NormalizedCultureInfo.ShouldBeSameAs( NormalizedCultureInfo.CodeDefault );
        p.MCString.ShouldBeSameAs( MCString.Empty );
        p.CodeString.ShouldBeSameAs( CodeString.Empty );

        p.NExtendedCultureInfo.ShouldBeNull();
        p.NNormalizedCultureInfo.ShouldBeNull();
        p.NSimpleUserMessage.ShouldBeNull();
        p.NUserMessage.ShouldBeNull();
        p.NMCString.ShouldBeNull();
        p.NCodeString.ShouldBeNull();
        p.NFormattedString.ShouldBeNull();
        p.NUserMessage.HasValue.ShouldBeFalse();
    }
}
