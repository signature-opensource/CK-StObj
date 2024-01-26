using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public class RecordAnonymousTests
    {
        public interface IWithTuple : IPoco
        {
            ref (string, int) Hop { get; }
        }

        [Test]
        public void simple_tuple_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithTuple ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithTuple>>();
            var o = f.Create( o => { o.Hop = ("CodeGen!", 3712); } );
            o.ToString().Should().Be( @"{""Hop"":[""CodeGen!"",3712]}" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( ("CodeGen!", 3712) );
        }

        public interface IWithNullableTuple : IPoco
        {
            ref (string, int)? Hop { get; }
        }

        [Test]
        public void simple_nullable_tuple_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithNullableTuple ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithNullableTuple>>();
            var o = f.Create( o => { o.Hop = ("CodeGen!", 3712); } );
            o.ToString().Should().Be( @"{""Hop"":[""CodeGen!"",3712]}" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( ("CodeGen!", 3712) );

            o.Hop = null;
            o.ToString().Should().Be( @"{""Hop"":null}" );

            var o3 = JsonTestHelper.Roundtrip( directory, o );
            o3.Hop.Should().BeNull();
        }

        public interface IWithCollections : IPoco
        {
            IList<(string, int)> List { get; }
            ISet<(string, int)> Set { get; }
            IDictionary<string, (string, int)> Dic { get; }
        }

        [Test]
        public void IPoco_collections_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithCollections ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithCollections>>();
            var o = f.Create( o =>
            {
                o.List.Add( ("C1", 3712) );
                o.Set.Add( ("C2", 42) );
                o.Dic.Add( "one", ("CodeGen!", 1789) );
            } );
            o.ToString().Should().Be( """{"List":[["C1",3712]],"Set":[["C2",42]],"Dic":{"one":["CodeGen!",1789]}}""" );

            JsonTestHelper.Roundtrip( directory, o );
        }

        public record struct Rec( List<(int, Guid)> List, HashSet<(int, Guid)> Set, Dictionary<string, (int, Guid)> Dic );

        public interface IWithRecord : IPoco
        {
            IList<Rec> Records { get; }
        }

        [Test]
        public void collections_of_records_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithRecord ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithRecord>>();

            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var g3 = Guid.NewGuid();
            var o = f.Create( o =>
            {
                o.Records.Add( new Rec( new List<(int, Guid)> { (1, g1) },
                               new HashSet<(int, Guid)> { (2, g2) },
                               new Dictionary<string, (int, Guid)>() { { "1", (3, g3) } } ) );
            } );
            o.ToString().Should().Be( $$$"""{"Records":[{"List":[[1,"{{{g1}}}"]],"Set":[[2,"{{{g2}}}"]],"Dic":{"1":[3,"{{{g3}}}"]}}]}""" );

            JsonTestHelper.Roundtrip( directory, o );
        }
        public record struct RecNullable( List<(int, Guid)?> List, HashSet<(int, Guid)?> Set, Dictionary<string, (int, Guid)?> Dic );

        public interface IWithRecordNullable : IPoco
        {
            IList<RecNullable> Records { get; }
        }

        [Test]
        public void collections_of_nullable_records_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithRecordNullable ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithRecordNullable>>();

            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var g3 = Guid.NewGuid();
            var o = f.Create( o =>
            {
                o.Records.Add( new RecNullable( new List<(int, Guid)?> { (1, g1), null },
                               new HashSet<(int, Guid)?> { (2, g2), null },
                               new Dictionary<string, (int, Guid)?>() { { "1", (3, g3) }, { "2", null } } ) );
            } );
            o.ToString().Should().Be( $$$"""{"Records":[{"List":[[1,"{{{g1}}}"],null],"Set":[[2,"{{{g2}}}"],null],"Dic":{"1":[3,"{{{g3}}}"],"2":null}}]}""" );

            JsonTestHelper.Roundtrip( directory, o );
        }


    }
}
