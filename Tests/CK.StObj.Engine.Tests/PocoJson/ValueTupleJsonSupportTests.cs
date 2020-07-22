using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class ValueTupleJsonSupportTests
    {
        public interface IWithTuple : IPoco
        {
            (string, int) Hop { get; set; }
        }

        [Test]
        public void simple_tuple_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithTuple ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<IWithTuple>>();
            var o = f.Create( o => { o.Hop = ("CodeGen!", 3712); } );
            var o2 = PocoJsonTests.Roundtrip( s, o );

            Debug.Assert( o2 != null );
            o2.Hop.Should().Be( ("CodeGen!", 3712) );
        }

        public interface IWithNullableTuple : IPoco
        {
            (string, int)? Hop { get; set; }
        }

        [Test]
        public void simple_nullable_tuple_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithNullableTuple ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<IWithNullableTuple>>();
            var o = f.Create( o => { o.Hop = ("CodeGen!", 3712); } );
            var o2 = PocoJsonTests.Roundtrip( s, o );

            Debug.Assert( o2 != null );
            o2.Hop.Should().Be( ("CodeGen!", 3712) );

            o.Hop = null;

            var o3 = PocoJsonTests.Roundtrip( s, o );
            o3.Hop.Should().BeNull();
        }



    }
}
