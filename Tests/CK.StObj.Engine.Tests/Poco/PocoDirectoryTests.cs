using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;


namespace CK.StObj.Engine.Tests.Poco
{

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
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
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
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
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
                var c = TestHelper.CreateStObjCollector( typeof( ICmdBadName1 ) );
                TestHelper.GetFailedResult( c, "Duplicate ExternalName in attribute on " );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( ICmdBadName2 ) );
                TestHelper.GetFailedResult( c, "Duplicate ExternalName in attribute on " );
            }
        }

        public interface ICmdNoName : IPoco { }

        public interface ICmdNoNameA : ICmdNoName { }

        public interface ICmdNoNameB : ICmdNoName { }

        public interface ICmdNoNameC : ICmdNoNameA, ICmdNoNameB { }

        [Test]
        public void when_no_PocoName_is_defined_the_Poco_uses_its_PrimaryInterface_FullName()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdNoName ), typeof( ICmdNoNameA ), typeof( ICmdNoNameB ), typeof( ICmdNoNameC ) );
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                TestHelper.CompileAndLoadStObjMap( c ).Map.Should().NotBeNull();
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
            var c = TestHelper.CreateStObjCollector( typeof( ICmdSecondary ) );
            TestHelper.GetFailedResult( c, $"ExternalName attribute appear on '{typeof( ICmdSecondary ).ToCSharpName()}'." );
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
            var c = TestHelper.CreateStObjCollector( typeof( ICmd1 ), typeof( ICmd1Bis ) );
            TestHelper.GetFailedResult( c, "The Poco name 'Cmd1' clashes: both '" );
        }
    }
}

