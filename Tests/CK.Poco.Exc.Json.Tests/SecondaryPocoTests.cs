using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public partial class SecondaryPocoTests
    {
        public interface IBasic : IPoco
        {
            int Power { get; set; }
        }

        public interface ISecondary : IBasic
        {
            string Name { get; set; }
        }

        public interface IWithSecondaryProperty : IPoco
        {
            /// <summary>
            /// Unitialized because nullable.
            /// </summary>
            ISecondary? NullableSecondary { get; set; }

            /// <summary>
            /// Initialized because non-nullable.
            /// </summary>
            ISecondary SecondaryWithSetter { get; set; }

            /// <summary>
            /// Initialized.
            /// </summary>
            ISecondary Secondary { get; }
        }

        [Test]
        public void secondary_properties()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( ISecondary ), typeof( IWithSecondaryProperty ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var s1 = directory.Create<ISecondary>( s => { s.Power = 3712; s.Name = "Talia"; } );
            var s2 = directory.Create<ISecondary>( s => { s.Power = -1; s.Name = "Olly"; } );
            var holder = directory.Create<IWithSecondaryProperty>( h =>
            {
                h.Secondary.Power = 42;
                h.Secondary.Name = "Albert";
                h.SecondaryWithSetter = s1;
                h.NullableSecondary = s2;
            } );

            holder.ToString().Should().Be( """
                {"NullableSecondary":{"Power":-1,"Name":"Olly"},"SecondaryWithSetter":{"Power":3712,"Name":"Talia"},"Secondary":{"Power":42,"Name":"Albert"}}
                """ );

            JsonTestHelper.Roundtrip( directory, holder );
        }

        public interface IOtherSecondary : IBasic
        {
            string Id { get; set; }
        }

        public interface IWithCollections : IPoco
        {
            IList<ISecondary> List1 { get; }
            List<ISecondary> ConcreteList1 { get; set; }
            IDictionary<int, ISecondary> Dictionary1 { get; }
            Dictionary<int, ISecondary> ConcreteDictionary1 { get; set; }
            ISecondary[] Array1 { get; set; }

            IList<IOtherSecondary> List2 { get; }
            List<IOtherSecondary> ConcreteList2 { get; set; }
            IDictionary<int, IOtherSecondary> Dictionary2 { get; }
            Dictionary<int, IOtherSecondary> ConcreteDictionary2 { get; set; }

            IOtherSecondary[] Array2 { get; set; }
        }

        [Test]
        public void secondary_collections()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ),
                                                     typeof( ISecondary ),
                                                     typeof( IOtherSecondary ),
                                                     typeof( IWithCollections ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var s1 = directory.Create<ISecondary>( s => { s.Power = 3712; s.Name = "Talia"; } );
            var s2 = directory.Create<IOtherSecondary>( s => { s.Power = -1; s.Id = "#1"; } );
            var holder = directory.Create<IWithCollections>( h =>
            {
                h.List1.Add( s1 );
                h.ConcreteList1.Add( s1 );
                h.Dictionary1.Add( 1, s1 );
                h.ConcreteDictionary1.Add( 12, s1 );
                h.Array1 = new[] { s1 };

                h.List2.Add( s2 );
                h.ConcreteList2.Add( s2 );
                h.Dictionary2.Add( 2, s2 );
                h.ConcreteDictionary2.Add( 22, s2 );
                h.Array2 = new[] { s2 };
            } );

            holder.ToString().Should().Be( """
                {
                    "List1":[{"Power":3712,"Name":"Talia","Id":""}],
                    "ConcreteList1":[{"Power":3712,"Name":"Talia","Id":""}],
                    "Dictionary1":[[1,{"Power":3712,"Name":"Talia","Id":""}]],
                    "ConcreteDictionary1":[[12,{"Power":3712,"Name":"Talia","Id":""}]],
                    "Array1":[{"Power":3712,"Name":"Talia","Id":""}],
                    "List2":[{"Power":-1,"Name":"","Id":"#1"}],
                    "ConcreteList2":[{"Power":-1,"Name":"","Id":"#1"}],
                    "Dictionary2":[[2,{"Power":-1,"Name":"","Id":"#1"}]],
                    "ConcreteDictionary2":[[22,{"Power":-1,"Name":"","Id":"#1"}]],
                    "Array2":[{"Power":-1,"Name":"","Id":"#1"}]
                }
                """.Replace( " ", "" ).ReplaceLineEndings( "" ) );

            JsonTestHelper.Roundtrip( directory, holder );
        }



    }
}
