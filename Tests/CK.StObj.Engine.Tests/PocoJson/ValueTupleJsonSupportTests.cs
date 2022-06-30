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
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithTuple>>();
            var o = f.Create( o => { o.Hop = ("CodeGen!", 3712); } );
            var o2 = JsonTestHelper.Roundtrip( directory, o );
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
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithNullableTuple>>();
            var o = f.Create( o => { o.Hop = ("CodeGen!", 3712); } );
            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( ("CodeGen!", 3712) );

            o.Hop = null;

            var o3 = JsonTestHelper.Roundtrip( directory, o );
            o3.Hop.Should().BeNull();
        }

        [ExternalName("WithHierarchies")]
        public interface ITupleWithHierachies : IPoco
        {
            (int, Person, Teacher, Student) Hop { get; set; }
        }


        [Test]
        public void tuple_with_external_types()
        {
            // We use IPocoAllOfThem to make sure that Intern is allowed.
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( JsonStringParseSupport ), typeof( IPocoAllOfThem ), typeof( ITupleWithHierachies ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var o = s.GetRequiredService<IPocoFactory<ITupleWithHierachies>>().Create();
            o.Hop = (3712, new Intern( "I", "i", null ), new Teacher( "T", "level" ), new Student( "S", 3 ));

            string? serialized = null;
            var o2 = JsonTestHelper.Roundtrip( directory, o, text: t => serialized = t );
            o2.Should().BeEquivalentTo( o );

            // The Student is NOT typed since it is final.
            serialized.Should().Be( "[\"WithHierarchies\",{\"hop\":[3712,[\"CI:CT\",\"I|i|null\"],[\"CT:CP\",\"T|level\"],\"S|3\"]}]" );
        }

        [ExternalName( "ComplexTuple" )]
        public interface IComplexTuple : IPoco
        {
            (int, string?, string?, object?, List<string?>?, object?) Hop { get; set; }
        }

        [Test]
        public void complex_tuple()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IComplexTuple ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var o = s.GetRequiredService<IPocoFactory<IComplexTuple>>().Create();
            string? serialized = null;
            var o2 = JsonTestHelper.Roundtrip( directory, o, text: t => serialized = t );
            o2.Should().BeEquivalentTo( o );

            serialized.Should().Be( "[\"ComplexTuple\",{\"hop\":[0,null,null,null,null,null]}]" );

            o.Hop = (5, "Albert", null, null, new List<string?>() { "X", null, "Y" }, 37.12);
            o2 = JsonTestHelper.Roundtrip( directory, o, text: t => serialized = t );
            o2.Should().BeEquivalentTo( o );
            serialized.Should().Be( "[\"ComplexTuple\",{\"hop\":[5,\"Albert\",null,null,[\"X\",null,\"Y\"],37.12]}]" );

        }

    }
}
