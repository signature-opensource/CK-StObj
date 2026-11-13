using CK.Core;
using CK.Setup;
using NUnit.Framework;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.AppBuilder.Tests;

[TestFixture]
public class AppConfigurationTests
{
    // A solution folder with a "Backs.Builder" project and the .App/.Host projects of the given BinPaths.
    static NormalizedPath CreateSolution( string ckonf, params string[] binPaths )
    {
        NormalizedPath root = Path.Combine( Path.GetTempPath(), "CK.AppBuilder.Tests", Guid.NewGuid().ToString( "N" ) );
        Directory.CreateDirectory( root );
        File.WriteAllText( root.AppendPart( "Sample.slnx" ), "<Solution />" );
        var builder = root.AppendPart( "Backs.Builder" );
        Directory.CreateDirectory( builder );
        File.WriteAllText( builder.AppendPart( "ckonf.json" ), ckonf );
        foreach( var b in binPaths )
        {
            Directory.CreateDirectory( root.AppendPart( $"{b}.App" ) );
            Directory.CreateDirectory( root.AppendPart( $"{b}.Host" ) );
        }
        return builder;
    }

    static AppConfiguration? Load( NormalizedPath builder, out EngineConfiguration? engine, XElement? ckSetup = null )
        => AppConfiguration.Load( TestHelper.Monitor, builder, "bin/Debug/net10.0", ckSetup, out engine );

    [Test]
    public void the_BinPaths_are_keyed_by_name_and_their_projects_follow_the_convention()
    {
        var builder = CreateSolution( """
            {
              // Comments are allowed.
              "App": {
                "BinPaths": {
                  "Back2": {
                    "CompileOption": "Parse",
                    "DiscoverAssembliesFromPath": true,
                    "Types": [ "CK.Core.IActivityMonitor, CK.ActivityMonitor",
                               { "Name": "CK.Core.IParallelLogger, CK.ActivityMonitor", "Kind": "IsContainerConfiguredService|IsScoped" } ],
                    "CK-AppIdentity": { "FullName": "Sample/$Back2" }
                  },
                  "Back1": { "CK-AppIdentity": { "FullName": "Sample/$Back1" } }
                }
              }
            }
            """, "Back1", "Back2" );
        var root = builder.RemoveLastPart();
        var app = Load( builder, out var engine ).ShouldNotBeNull();
        engine.ShouldNotBeNull();

        // Sorted by name: their order in the file means nothing.
        app.BinPaths.Select( b => b.Name ).ShouldBe( ["Back1", "Back2"] );
        engine.BinPaths.Select( b => b.Name ).ShouldBe( ["Back1", "Back2"] );
        engine.FirstBinPath.Name.ShouldBe( "Back1" );

        var back2 = app.BinPaths[1];
        back2.AppProjectPath.ShouldBe( root.AppendPart( "Back2.App" ) );
        back2.HostProjectPath.ShouldBe( root.AppendPart( "Back2.Host" ) );
        back2.BinPathConfiguration.Path.ShouldBe( root.AppendPart( "Back2.App" ).Combine( "bin/Debug/net10.0" ) );
        back2.BinPathConfiguration.ProjectPath.ShouldBe( root.AppendPart( "Back2.Host" ) );
        back2.BinPathConfiguration.CompileOption.ShouldBe( CompileOption.Parse );
        back2.BinPathConfiguration.DiscoverAssembliesFromPath.ShouldBeTrue();
        back2.BinPathConfiguration.Types.Count.ShouldBe( 2 );
        back2.BinPathConfiguration.Types.Single( t => t.Type == typeof( IParallelLogger ) ).Kind
             .ShouldBe( ConfigurableAutoServiceKind.IsContainerConfiguredService | ConfigurableAutoServiceKind.IsScoped );
        back2.Configuration["CK-AppIdentity:FullName"].ShouldBe( "Sample/$Back2" );
    }

    [Test]
    public void the_hosts_appsettings_are_projections_without_the_compile_keys()
    {
        var builder = CreateSolution( """
            {
              "App": {
                "BinPaths": {
                  "Front": {
                    "Types": [ "CK.Core.IActivityMonitor, CK.ActivityMonitor" ],
                    "CK-AppIdentity": {
                      "FullName": "Sample/$Front",
                      "Local": { "PartyClient": { "App": { "Type": "TypeScript", "Kind": "DefaultApp", "StaticFiles": { "AssetsMaxAge": "01:00:00" } } } }
                    },
                    "ConnectionStrings": { "Db": "Server=.;Database=Sample;Trust='yes'" }
                  }
                }
              }
            }
            """, "Front" );
        var app = Load( builder, out _ ).ShouldNotBeNull();
        var front = app.BinPaths.Single();
        // A contributor declares the compile time keys of the sections it handles.
        front.AddCompileKey( "CK-AppIdentity:Local:PartyClient:App:Type" );
        front.AddCompileKey( "CK-AppIdentity:Local:PartyClient:App:Kind" );
        app.WriteProjections( TestHelper.Monitor ).ShouldBeTrue();

        var file = front.HostProjectPath.AppendPart( "appsettings.json" );
        var text = File.ReadAllText( file );
        text.ShouldStartWith( "// Generated from Backs.Builder/ckonf.json: do not edit." );
        text.ShouldNotContain( "Types" );
        text.ShouldNotContain( "\"Type\"" );
        text.ShouldNotContain( "DefaultApp" );
        text.ShouldContain( "\"AssetsMaxAge\": \"01:00:00\"" );
        // No escaping of the quotes in the values.
        text.ShouldContain( "Trust='yes'" );
        var projected = ImmutableConfigurationSection.CreateFromJson( "appsettings", text );
        projected["CK-AppIdentity:FullName"].ShouldBe( "Sample/$Front" );

        // Unchanged: not written again.
        var written = File.GetLastWriteTimeUtc( file );
        System.Threading.Thread.Sleep( 50 );
        app.WriteProjections( TestHelper.Monitor ).ShouldBeTrue();
        File.GetLastWriteTimeUtc( file ).ShouldBe( written );
    }

    [Test]
    public void ProjectCompileKeys_keeps_the_compile_keys_of_the_leaves()
    {
        var builder = CreateSolution( """
            {
              "App": {
                "ProjectCompileKeys": true,
                "BinPaths": { "Front": { "Types": [], "Local": { "Kind": "DefaultApp" } } }
              }
            }
            """, "Front" );
        var app = Load( builder, out _ ).ShouldNotBeNull();
        app.BinPaths[0].AddCompileKey( "Local:Kind" );
        app.WriteProjections( TestHelper.Monitor ).ShouldBeTrue();
        var text = File.ReadAllText( app.BinPaths[0].HostProjectPath.AppendPart( "appsettings.json" ) );
        text.ShouldContain( "DefaultApp" );
        // The keys of the BinPath itself are never projected.
        text.ShouldNotContain( "Types" );
    }

    [Test]
    public void AppProject_and_HostProject_depart_from_the_convention()
    {
        var builder = CreateSolution( """
            { "App": { "BinPaths": { "Back": { "AppProject": "../Other/Back.App", "HostProject": "../Back.Host" } } } }
            """, "Back" );
        var root = builder.RemoveLastPart();
        Directory.CreateDirectory( root.Combine( "Other/Back.App" ) );
        var app = Load( builder, out _ ).ShouldNotBeNull();
        app.BinPaths[0].AppProjectPath.ShouldBe( root.Combine( "Other/Back.App" ) );
    }

    [TestCase( """{ "App": { "BinPaths": { "Back": { "HostProject": "../../Outside.Host" } } } }""", "leaves the solution's folder" )]
    [TestCase( """{ "App": { "BinPaths": { "Missing": {} } } }""", "doesn't exist (by convention: 'Missing.App'" )]
    [TestCase( """{ "App": { "BinPaths": { "Back": {}, "back": {} } } }""", "Duplicate key 'App:BinPaths:back'" )]
    [TestCase( """{ "App": { "BinPaths": [ { "Back": {} } ] } }""", "must be an object" )]
    [TestCase( """{ "App": { "BinPaths": {} } }""", "at least one BinPath" )]
    [TestCase( """{ "Other": 1, "App": { "BinPaths": { "Back": {} } } }""", "Unknown key 'Other'" )]
    [TestCase( """{ "App": { "Name": "X", "BinPaths": { "Back": {} } } }""", "Unknown key 'App:Name'" )]
    [TestCase( """{ "App": { "ProjectCompileKeys": "maybe", "BinPaths": { "Back": {} } } }""", "Invalid \"App:ProjectCompileKeys\"" )]
    public void invalid_configurations_are_errors( string ckonf, string expected )
    {
        var builder = CreateSolution( ckonf, "Back" );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            Load( builder, out var engine ).ShouldBeNull();
            engine.ShouldBeNull();
            texts.ShouldContain( t => t.Contains( expected ), $"Expected: {expected}" );
        }
    }

    [Test]
    public void the_CKSetup_xml_brings_the_root_but_no_BinPath()
    {
        var builder = CreateSolution( """{ "App": { "BinPaths": { "Back": {} } } }""", "Back" );
        var withRoot = XElement.Parse( "<Setup><TraceDependencySorterInput>true</TraceDependencySorterInput></Setup>" );
        Load( builder, out var engine, withRoot ).ShouldNotBeNull();
        engine.ShouldNotBeNull().TraceDependencySorterInput.ShouldBeTrue();

        var withBinPaths = XElement.Parse( "<Setup><BinPaths><BinPath Path=\"../Back.App/bin/Debug/net10.0\" /></BinPaths></Setup>" );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            Load( builder, out _, withBinPaths ).ShouldBeNull();
            texts.ShouldContain( t => t.Contains( "defines BinPaths" ) );
        }
    }
}
