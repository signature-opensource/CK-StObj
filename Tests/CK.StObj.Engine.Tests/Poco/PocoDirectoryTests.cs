using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using static CK.Testing.MonitorTestHelper;


namespace CK.StObj.Engine.Tests.Poco;


[TestFixture]
public class PocoDirectoryTests
{
    [ExternalName( "Test", "PreviousTest1", "PreviousTest2" )]
    public interface ICmdTest : IPoco
    {
    }

    [Test]
    public void simple_Poco_found_by_name_previous_name_or_interface_type()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ICmdTest ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var f0 = d.Find( "Test" );
        var f1 = d.Find( "PreviousTest1" );
        var f2 = d.Find( "PreviousTest2" );
        f0.Should().NotBeNull().And.BeSameAs( f1 ).And.BeSameAs( f2 );
        var f3 = d.Find( typeof( ICmdTest ) );
        f3.Should().NotBeNull().And.BeSameAs( f0 );

        // Typed helper.
        d.Find<ICmdTest>().Should().NotBeNull().And.BeSameAs( f0 );
    }

    [Test]
    public void GeneratedPoco_factory_instance_can_be_found_by_its_type()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ICmdTest ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var p = d.Create<ICmdTest>();
        d.Find( p.GetType() ).Should().NotBeNull().And.BeSameAs( ((IPocoGeneratedClass)p).Factory );
    }

    [ExternalName( "Test", "Prev1", "Test" )]
    public interface ICmdBadName1 : IPoco { }

    [ExternalName( "Test", "Test" )]
    public interface ICmdBadName2 : IPoco { }

    [Test]
    public void duplicate_names_on_the_Poco_are_errors()
    {
        {
            TestHelper.GetFailedCollectorResult( [typeof( ICmdBadName1 )], "Duplicate ExternalName in attribute on " );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( ICmdBadName2 )], "Duplicate ExternalName in attribute on " );
        }
    }

    public interface ICmdNoName : IPoco { }

    public interface ICmdNoNameA : ICmdNoName { }

    public interface ICmdNoNameB : ICmdNoName { }

    public interface ICmdNoNameC : ICmdNoNameA, ICmdNoNameB { }

    [Test]
    public void when_no_PocoName_is_defined_the_Poco_uses_its_PrimaryInterface_FullName()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ICmdNoName ), typeof( ICmdNoNameA ), typeof( ICmdNoNameB ), typeof( ICmdNoNameC ) );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
        {
            configuration.RunSuccessfully();
            entries.Where( x => x.MaskedLevel == LogLevel.Warn )
                   .Select( x => x.Text )
                   .Should()
                   .Contain( $"Type '{typeof( ICmdNoName ).ToCSharpName()}' use its full CSharpName as its name since no [ExternalName] attribute is defined." );
        }
    }

    [ExternalName( "NoWay" )]
    public interface ICmdSecondary : ICmdNoName
    {
    }

    [Test]
    public void PocoName_attribute_must_be_on_the_primary_interface()
    {
        var c = new[] { typeof( ICmdSecondary ) };
        TestHelper.GetFailedCollectorResult( c, $"ExternalName attribute appear on '{typeof( ICmdSecondary ).ToCSharpName( false )}'." );
    }

    [ExternalName( "Cmd1" )]
    public interface ICmd1 : IPoco
    {
    }

    [ExternalName( "Cmd1" )]
    public interface ICmd1Bis : IPoco
    {
    }

    [Test]
    public void PocoName_must_be_unique()
    {
        TestHelper.GetFailedCollectorResult( [typeof( ICmd1 ), typeof( ICmd1Bis )], "The Poco name 'Cmd1' clashes: both '" );
    }
}

