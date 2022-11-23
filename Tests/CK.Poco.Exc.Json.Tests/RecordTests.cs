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

            //var o2 = JsonTestHelper.Roundtrip( directory, o );
            //o2.Hop.Should().Be( ("CodeGen!", 3712) );
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

            //var o2 = JsonTestHelper.Roundtrip( directory, o );
            //o2.Hop.Should().Be( ("CodeGen!", 3712) );

            o.Hop = null;
            o.ToString().Should().Be( @"{""Hop"":null}" );

            //var o3 = JsonTestHelper.Roundtrip( directory, o );
            //o3.Hop.Should().BeNull();
        }

    }
}
