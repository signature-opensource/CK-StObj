using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
    public void default_for_Globalization_types_are_handled()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add(typeof( IWithGlobalization ));
        using var auto = configuration.Run().CreateAutomaticServices();

        var p = auto.Services.GetRequiredService<IPocoFactory<IWithGlobalization>>().Create();

        p.ExtendedCultureInfo.Should().BeSameAs( NormalizedCultureInfo.CodeDefault );
        p.NormalizedCultureInfo.Should().BeSameAs( NormalizedCultureInfo.CodeDefault );
        p.MCString.Should().BeSameAs( MCString.Empty );
        p.CodeString.Should().BeSameAs( CodeString.Empty );

        p.NExtendedCultureInfo.Should().BeNull();
        p.NNormalizedCultureInfo.Should().BeNull();
        p.NSimpleUserMessage.Should().BeNull();
        p.NUserMessage.Should().BeNull();
        p.NMCString.Should().BeNull();
        p.NCodeString.Should().BeNull();
        p.NFormattedString.Should().BeNull();
        p.NUserMessage.HasValue.Should().BeFalse();
    }
}
