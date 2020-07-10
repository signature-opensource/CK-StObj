using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;


namespace CK.StObj.Engine.Tests.Poco
{

    [TestFixture]
    public class PocoDirectoryTests
    {
        [PocoName( "Test", "PreviousTest1", "PreviousTest2" )]
        public interface ICmdTest : IPoco
        {
        }

        [Test]
        public void simple_Poco()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdTest ) );
            var d = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<PocoDirectory>();
            var f0 = d.Find( "Test" );
            var f1 = d.Find( "PreviousTest1" );
            var f2 = d.Find( "PreviousTest2" );
            f0.Should().NotBeNull().And.BeSameAs( f1 ).And.BeSameAs( f2 );
        }

        [PocoName( "Test", "Prev1", "Test" )]
        public interface ICmdBadName1 : IPoco { }

        [PocoName( "Test", "Test" )]
        public interface ICmdBadName2 : IPoco { }

        [Test]
        public void duplicate_names_on_the_Poco_are_errors()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( ICmdBadName1 ) );
                using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                                .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                         && x.Text.StartsWith( "Duplicate PocoName in attribute " ) ) ) ) )
                {
                    TestHelper.GetFailedResult( c );
                }
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( ICmdBadName2 ) );
                using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                                .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                         && x.Text.StartsWith( "Duplicate PocoName in attribute " ) ) ) ) )
                {
                    TestHelper.GetFailedResult( c );
                }
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
            using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                            .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Warn
                                                                     && x.Text.StartsWith( $"Poco '{typeof( ICmdNoName ).FullName}' use its full name " ) ) ) ) )
            {
                TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeTrue();
            }
        }

        [PocoName( "NoWay" )]
        public interface ICmdSecondary : ICmdNoName
        {
        }

        [Test]
        public void PocoName_attribute_must_be_on_the_primary_interface()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmdSecondary ) );
            using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                            .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                     && x.Text.StartsWith( $"PocoName attribute appear on '{typeof( ICmdSecondary ).FullName}'." ) ) ) ) )
            {
                TestHelper.GetFailedResult( c );
            }
        }

        [PocoName( "Cmd1" )]
        public interface ICmd1 : IPoco
        {
        }

        [PocoName( "Cmd1" )]
        public interface ICmd1Bis : IPoco
        {
        }

        [Test]
        public void PocoName_must_be_unique()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICmd1 ), typeof( ICmd1Bis ) );
            using( TestHelper.Monitor.CollectEntries( entries => entries.Should()
                                            .Match( e => e.Any( x => x.MaskedLevel == LogLevel.Error
                                                                     && x.Text.StartsWith( "The Poco name 'Cmd1' clashes: both '" ) ) ) ) )
            {
                TestHelper.GetFailedResult( c );
            }
        }
    }
}

