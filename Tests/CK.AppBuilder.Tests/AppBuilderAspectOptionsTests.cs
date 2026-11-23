using CK.Core;
using CK.Setup;
using CK.TypeSet;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.AppBuilder.Tests;

[AppAspect( "Test", TypeActivation.Optional )]
public sealed class TestAppAspect : IAppAspect
{
    public static TestAppAspect? Last;

    public TestAppAspect( IConfiguration? configuration, IReadOnlyList<IAppAspectOption<TestAppAspect>> options )
    {
        Configuration = configuration;
        Options = options;
        Last = this;
    }

    public IConfiguration? Configuration { get; }

    public IReadOnlyList<IAppAspectOption<TestAppAspect>> Options { get; }

    public bool Apply( IActivityMonitor monitor, AppBuilderContext context, AppConfiguration app ) => true;
}

// Enabled unless the aspect's section says false.
[AppAspectOption( "Header", TypeActivation.ByDefault )]
public sealed class HeaderAspectOption : IAppAspectOption<TestAppAspect>
{
}

// Enabled only when the aspect's section says so; it comes before Header (an optional relation orders, it doesn't require).
[AppAspectOption( "Main", TypeActivation.Optional )]
[OptionalRequiredBy<HeaderAspectOption>]
public sealed class MainAspectOption : IAppAspectOption<TestAppAspect>
{
    public MainAspectOption( IConfiguration? configuration )
    {
        Level = configuration?["Level"];
    }

    public string? Level { get; }
}

[TestFixture]
public class AppBuilderAspectOptionsTests
{
    static AppBuilderContext? Run( string aspects )
    {
        var engine = new EngineConfiguration();
        foreach( var t in new[] { typeof( TestAppAspect ), typeof( HeaderAspectOption ), typeof( MainAspectOption ) } ) engine.FirstBinPath.Types.Add( t );
        var sets = BinPathTypeGroup.Run( TestHelper.Monitor, engine ).ShouldNotBeNull();
        sets.Success.ShouldBeTrue();
        var section = ImmutableConfigurationSection.CreateFromJson( "ckonf:Apps", "Sample", $$"""{ "Aspects": {{aspects}} }""" );
        var context = new AppBuilderContext( engine, [new AppConfiguration( "Sample", TestHelper.TestProjectFolder, TestHelper.TestProjectFolder, section, engine.FirstBinPath )] );
        TestAppAspect.Last = null;
        return AppBuilderAspects.Run( TestHelper.Monitor, context, sets ) ? context : null;
    }

    [Test]
    public void the_options_of_an_aspect_are_selected_ordered_and_given_to_its_constructor()
    {
        Run( """{ "Test": true }""" ).ShouldNotBeNull();
        TestAppAspect.Last.ShouldNotBeNull().Options.Select( o => o.GetType() ).ShouldBe( [typeof( HeaderAspectOption )] );

        // Main comes before Header.
        Run( """{ "Test": { "Main": { "Level": "3" } } }""" ).ShouldNotBeNull();
        var options = TestAppAspect.Last.ShouldNotBeNull().Options;
        options.Select( o => o.GetType() ).ShouldBe( [typeof( MainAspectOption ), typeof( HeaderAspectOption )] );
        ((MainAspectOption)options[0]).Level.ShouldBe( "3" );
        TestAppAspect.Last.Configuration.ShouldNotBeNull();

        Run( """{ "Test": { "Main": true, "Header": false } }""" ).ShouldNotBeNull();
        TestAppAspect.Last.ShouldNotBeNull().Options.Select( o => o.GetType() ).ShouldBe( [typeof( MainAspectOption )] );

        // The aspect is optional: without its key, it is not created.
        Run( "{}" ).ShouldNotBeNull();
        TestAppAspect.Last.ShouldBeNull();
    }

    [Test]
    public void an_invalid_option_value_is_an_error()
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            Run( """{ "Test": { "Main": "yes" } }""" ).ShouldBeNull();
            texts.ShouldContain( t => t.Contains( "Invalid option", StringComparison.Ordinal ) && t.Contains( "Main", StringComparison.Ordinal ) );
        }
    }
}
