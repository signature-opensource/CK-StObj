using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Poco.Exc.Json.Tests.BasicTypeTests;
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithTuple ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithTuple>>();
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( CommonPocoJsonSupport ), typeof( IWithNullableTuple ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithNullableTuple>>();
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
            IList<(string Name, int Power)> List { get; }
            IList<(string, int)?> ListN { get; }
            ISet<(string, int)> Set { get; }
            ISet<(string, int)?> SetN { get; }
            IDictionary<string, (string, int)> Dic { get; }
            IDictionary<string, (string Code, int id)?> DicN { get; }
        }

        [Test]
        public void IPoco_collections_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( CommonPocoJsonSupport ), typeof( IWithCollections ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithCollections>>();
            var o = f.Create( o =>
            {
                o.List.Add( ("C1", 3712) );
                o.ListN.AddRangeArray( ("N1", 42), null );
                o.Set.Add( ("C2", 42) );
                o.SetN.AddRangeArray( ("N2", -2), null );
                o.Dic.Add( "one", ("CodeGen!", 1789) );
                o.DicN.Add( "two", ("CodeGen!", 12) );
                o.DicN.Add( "three", null );
            } );
            o.ToString().Should().Be( """
                {
                    "List": [["C1",3712]],
                    "ListN": [["N1", 42], null],
                    "Set": [["C2",42]],
                    "SetN": [["N2",-2], null],
                    "Dic":
                        {
                            "one": ["CodeGen!",1789]
                        },
                    "DicN":
                        {
                            "two": ["CodeGen!",12],
                            "three": null
                        }
                }
                """.Replace( " ", "" ).ReplaceLineEndings( "" ) );

            JsonTestHelper.Roundtrip( directory, o );
        }

        public record struct Rec( List<(int A, Guid)> List, HashSet<(int, Guid B)> Set, Dictionary<string, (int, Guid)> Dic );

        public interface IWithRecord : IPoco
        {
            IList<Rec> Records { get; }
        }

        [Test]
        public void collections_of_records_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( CommonPocoJsonSupport ), typeof( IWithRecord ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithRecord>>();

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
        public record struct RecNullable( List<(int A, Guid)?> List, HashSet<(int, Guid B)?> Set, Dictionary<string, (int C, Guid D)?> Dic );

        public interface IWithRecordNullable : IPoco
        {
            IList<RecNullable> Records { get; }
        }

        [Test]
        public void collections_of_nullable_records_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( CommonPocoJsonSupport ), typeof( IWithRecordNullable ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithRecordNullable>>();

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
