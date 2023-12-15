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

    }
}
