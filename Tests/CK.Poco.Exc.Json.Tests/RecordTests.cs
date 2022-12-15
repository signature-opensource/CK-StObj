using CK.Core;
using CK.Setup;
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
    public class RecordTests
    {
        public record struct Thing( string Name, int Count );

        public interface IWithRecord : IPoco
        {
            ref Thing Hop { get; }
        }

        [Test]
        public void simple_tuple_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IWithRecord ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithRecord>>();
            var o = f.Create( o =>
            {
                o.Hop.Name = "Albert";
                o.Hop.Count = 3712;
            } );
            o.ToString().Should().Be( @"{""Hop"":{""Name"":""Albert"",""Count"":3712}}" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( new Thing( "Albert", 3712 ) );
        }

        public interface IWithNullableRecord : IPoco
        {
            ref Thing? Hop { get; }
        }

        [Test]
        public void simple_nullable_tuple_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IWithNullableRecord ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithNullableRecord>>();
            var o = f.Create( o => { o.Hop = new Thing("Hip", 42); } );
            o.ToString().Should().Be( @"{""Hop"":{""Name"":""Hip"",""Count"":42}}" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( new Thing( "Hip", 42 ) );

            o.Hop = null;
            o.ToString().Should().Be( @"{""Hop"":null}" );

            var o3 = JsonTestHelper.Roundtrip( directory, o );
            o3.Hop.Should().BeNull();
        }

        public interface IHoldRecList : IPoco
        {
            public record struct Rec( IList<Rec> R, int A = 3712 );

            ref Rec P { get; }
        }

        [Test]
        public void recursive_list_use_of_named_record_is_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IHoldRecList ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var p = directory.Create<IHoldRecList>();
            p.P.R.Should().NotBeNull();
            p.P.A.Should().Be( 3712 );
            p.P.R.Add( new IHoldRecList.Rec( new List<IHoldRecList.Rec>(), 42 ) );

            p.ToString().Should().Be( @"{""P"":{""R"":[{""R"":[],""A"":42}],""A"":3712}}" );

            var p2 = JsonTestHelper.Roundtrip( directory, p );
            p2.Should().BeEquivalentTo( p );
        }
    }
}
