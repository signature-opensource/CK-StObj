using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class CollectionTests
    {
        public interface IListOfList : IPoco
        {
            List<List<int>> List { get; }
        }

        [Test]
        public void list_of_list_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IListOfList ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IListOfList>>();
            var oD = f.Create( o => { o.List.Add( new List<int> { 1, 2 } ); } );
            var oD2 = JsonTestHelper.Roundtrip( directory, oD );
            oD2.List[0].Should().HaveCount( 2 );
        }

        public interface IDic : IPoco
        {
            Dictionary<(int, int), string> StrangeBeast { get; }
        }

        [Test]
        public void dictionary_with_value_tuple_keys()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IDic ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var o = directory.Create<IDic>();
            o.StrangeBeast.Add( (0, 0), "First" );
            o.StrangeBeast.Add( (42, 3712), "Second" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.StrangeBeast.Should().BeEquivalentTo( o.StrangeBeast );
        }
        public interface IDicVKey : IPoco
        {
            Dictionary<int, string> ByInt { get; }
        }

        [Test]
        public void dictionary_with_int_keys()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IDicVKey ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var o = directory.Create<IDicVKey>();
            o.ByInt.Add( 0, "First" );
            o.ByInt.Add( 42, "Second" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.ByInt.Should().BeEquivalentTo( o.ByInt );
        }
    }
}
