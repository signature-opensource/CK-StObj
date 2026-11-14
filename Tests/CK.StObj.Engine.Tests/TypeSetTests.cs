using CK.Core;
using CK.Engine.TypeCollector;
using CK.Setup;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using System.Xml.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class TypeSetTests
{
    public class RootObject : IRealObject
    {
    }

    public class OtherObject : IRealObject
    {
    }

    [AlsoRegisterType<AlsoRegistered>]
    public class WithAlso : IRealObject
    {
    }

    public class AlsoRegistered : IRealObject
    {
    }

    public class AddedBySet : IRealObject
    {
    }

    [Test]
    public void a_set_is_the_types_of_the_BinPath_minus_its_excluded_types_plus_the_AlsoRegisterType_of_its_members()
    {
        var configuration = new EngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RootObject ) );
        configuration.FirstBinPath.Types.Add( typeof( OtherObject ) );
        configuration.FirstBinPath.Types.Add( typeof( WithAlso ) );
        var party = new TypeSetConfiguration( "$Party" );
        party.ExcludedTypes.Add( typeof( OtherObject ) );
        party.ExcludedTypes.Add( typeof( AlsoRegistered ) );
        configuration.FirstBinPath.AddTypeSet( party );

        var result = BinPathTypeGroup.Run( TestHelper.Monitor, configuration ).ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        var g = result.Groups.ShouldHaveSingleItem();
        g.ConfiguredTypes.AllTypes.Select( cT => cT.Type ).ShouldBe( [typeof( RootObject ), typeof( OtherObject ), typeof( WithAlso ), typeof( AlsoRegistered )], ignoreOrder: true );
        // AlsoRegistered is excluded, but [AlsoRegisterType] ignores the exclusions (as for the BinPath).
        g.TypeSets["$party"].Select( cT => cT.Type ).ShouldBe( [typeof( RootObject ), typeof( WithAlso ), typeof( AlsoRegistered )], ignoreOrder: true );
    }

    [Test]
    public void the_types_of_a_set_are_added_to_the_BinPath()
    {
        var configuration = new EngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RootObject ) );
        var client = new TypeSetConfiguration( "MiniApp" );
        client.Types.Add( typeof( AddedBySet ) );
        configuration.FirstBinPath.AddTypeSet( client );
        configuration.FirstBinPath.AddTypeSet( new TypeSetConfiguration( "Other" ) );

        var result = BinPathTypeGroup.Run( TestHelper.Monitor, configuration ).ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        var g = result.Groups.ShouldHaveSingleItem();
        g.ConfiguredTypes.AllTypes.Select( cT => cT.Type ).ShouldBe( [typeof( RootObject ), typeof( AddedBySet )], ignoreOrder: true );
        g.TypeSets["MiniApp"].Select( cT => cT.Type ).ShouldBe( [typeof( RootObject ), typeof( AddedBySet )], ignoreOrder: true );
        g.TypeSets["Other"].Count.ShouldBe( 2, "The inclusion propagates upward: the other sets have it too." );
    }

    [Test]
    public void a_set_cannot_add_a_type_that_the_BinPath_excludes()
    {
        var configuration = new EngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( RootObject ) );
        configuration.FirstBinPath.ExcludedTypes.Add( typeof( AddedBySet ) );
        var client = new TypeSetConfiguration( "MiniApp" );
        client.Types.Add( typeof( AddedBySet ) );
        configuration.FirstBinPath.AddTypeSet( client );

        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            BinPathTypeGroup.Run( TestHelper.Monitor, configuration ).ShouldBeNull();
            texts.ShouldContain( t => t.Contains( "adds the type" ) && t.Contains( "that the BinPath excludes: the exclusions apply downward." ) );
        }
    }

    [Test]
    public void the_sets_are_in_the_signature_and_the_xml()
    {
        static EngineConfiguration Create( bool exclude )
        {
            var c = new EngineConfiguration();
            c.FirstBinPath.Types.Add( typeof( RootObject ) );
            c.FirstBinPath.Types.Add( typeof( OtherObject ) );
            var s = new TypeSetConfiguration( "$Party" );
            if( exclude ) s.ExcludedTypes.Add( typeof( OtherObject ) );
            c.FirstBinPath.AddTypeSet( s );
            return c;
        }
        var a = BinPathTypeGroup.Run( TestHelper.Monitor, Create( false ) ).ShouldNotBeNull();
        var b = BinPathTypeGroup.Run( TestHelper.Monitor, Create( true ) ).ShouldNotBeNull();
        a.Signature.ShouldNotBe( b.Signature );

        var xml = Create( true ).ToXml();
        var back = new EngineConfiguration( xml );
        var set = back.FirstBinPath.FindTypeSet( "$party" ).ShouldNotBeNull();
        set.ExcludedTypes.ShouldBe( [typeof( OtherObject )] );
        XElement.DeepEquals( back.ToXml(), xml ).ShouldBeTrue();
    }

    [Test]
    public void a_set_can_have_a_parent()
    {
        var c = new EngineConfiguration();
        c.FirstBinPath.Types.Add( typeof( RootObject ) );
        c.FirstBinPath.AddTypeSet( new TypeSetConfiguration( "$Party" ) );
        c.FirstBinPath.AddTypeSet( new TypeSetConfiguration( "App" ) { Parent = "$Party" } );
        var back = new EngineConfiguration( c.ToXml() );
        back.FirstBinPath.FindTypeSet( "App" ).ShouldNotBeNull().Parent.ShouldBe( "$Party" );
        BinPathTypeGroup.Run( TestHelper.Monitor, back ).ShouldNotBeNull().Success.ShouldBeTrue();

        c.FirstBinPath.AddTypeSet( new TypeSetConfiguration( "Orphan" ) { Parent = "$Nobody" } );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            BinPathTypeGroup.Run( TestHelper.Monitor, c ).ShouldBeNull();
            texts.ShouldContain( t => t.Contains( "The parent '$Nobody' of TypeSet 'Orphan'" ) );
        }
    }
}