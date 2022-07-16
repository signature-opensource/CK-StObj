using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class PocoJsonWithJsonSerializerTests
    {
        public class SubObject
        {
            public int X { get; set; }
            public int Y { get; set; }
            public string Name { get; set; }
        }

        public class IndependentObjectWithIList
        {
            public IndependentObjectWithIList()
            {
                Name = String.Empty;
                Sub = new List<SubObject>();
            }

            public string Name { get; set; }

            public IList<SubObject> Sub { get; }
        }

        [Test]
        public void mixing_with_JsonSerializer_does_not_work_with_IList()
        {
            var o = new IndependentObjectWithIList() { Name = "Doe" };
            o.Sub.Add( new SubObject() { Name = "S", X = 37, Y = 12 } );

            byte[] bin;
            using( var m = new MemoryStream() )
            using( var w = new Utf8JsonWriter( m ) )
            {
                w.WriteStartObject();
                w.WriteBoolean( "working", true );
                w.WritePropertyName( "obj" );
                JsonSerializer.Serialize( w, o );
                w.WriteBoolean( "worked", false );
                w.WriteEndObject();
                w.Flush();
                bin = m.ToArray();
                var binText = Encoding.UTF8.GetString( m.ToArray() );
                binText.Should().Be( @"{""working"":true,""obj"":{""Name"":""Doe"",""Sub"":[{""X"":37,""Y"":12,""Name"":""S""}]},""worked"":false}" );
            }
            var r = new Utf8JsonReader( bin );
            r.Read(); // TokenType = None
            r.Read(); // StartObject
            r.GetString().Should().Be( "working" );
            r.Read(); // PropertyName
            r.GetBoolean().Should().BeTrue();
            r.Read(); 
            r.Read(); // PropertyName
            var o2 = JsonSerializer.Deserialize<IndependentObjectWithIList>( ref r );
            r.TokenType.Should().Be( JsonTokenType.EndObject );
            r.Read();

            r.GetString().Should().Be( "worked" );
            r.Read(); // PropertyName
            r.GetBoolean().Should().BeFalse();
            r.Read();
            r.TokenType.Should().Be( JsonTokenType.EndObject );
            r.Read();
            r.Read().Should().BeFalse();

            o2.Should().NotBeEquivalentTo( o, "Unfortunately... Sub IList is not filled." );
            o2!.Sub.Should().BeEmpty();
        }

        public class IndependentObjectWithReadonlyList
        {
            public IndependentObjectWithReadonlyList()
            {
                Name = String.Empty;
                Sub = new List<SubObject>();
            }

            public string Name { get; set; }

            public List<SubObject> Sub { get; }
        }

        [Test]
        public void mixing_with_JsonSerializer_does_not_work_with_readonly_List()
        {
            var o = new IndependentObjectWithReadonlyList() { Name = "Doe" };
            o.Sub.Add( new SubObject() { Name = "S", X = 37, Y = 12 } );

            byte[] bin;
            using( var m = new MemoryStream() )
            using( var w = new Utf8JsonWriter( m ) )
            {
                w.WriteStartObject();
                w.WriteBoolean( "working", true );
                w.WritePropertyName( "obj" );
                JsonSerializer.Serialize( w, o );
                w.WriteBoolean( "worked", false );
                w.WriteEndObject();
                w.Flush();
                bin = m.ToArray();
                var binText = Encoding.UTF8.GetString( m.ToArray() );
                binText.Should().Be( @"{""working"":true,""obj"":{""Name"":""Doe"",""Sub"":[{""X"":37,""Y"":12,""Name"":""S""}]},""worked"":false}" );
            }
            var r = new Utf8JsonReader( bin );
            r.Read(); // TokenType = None
            r.Read(); // StartObject
            r.GetString().Should().Be( "working" );
            r.Read(); // PropertyName
            r.GetBoolean().Should().BeTrue();
            r.Read();
            r.Read(); // PropertyName
            var o2 = JsonSerializer.Deserialize<IndependentObjectWithIList>( ref r );
            r.TokenType.Should().Be( JsonTokenType.EndObject );
            r.Read();

            r.GetString().Should().Be( "worked" );
            r.Read(); // PropertyName
            r.GetBoolean().Should().BeFalse();
            r.Read();
            r.TokenType.Should().Be( JsonTokenType.EndObject );
            r.Read();
            r.Read().Should().BeFalse();

            o2.Should().NotBeEquivalentTo( o, "Unfortunately... Sub List is not filled." );
            o2!.Sub.Should().BeEmpty();
        }
        public enum Status
        {
            None,
            One,
            Two
        }

        public class IndependentObject
        {
            public IndependentObject()
            {
                Name = String.Empty;
            }

            public Status Status { get; set; }

            public string Name { get; set; }

            public List<SubObject> Sub { get; set; }
        }

        [Test]
        public void mixing_with_JsonSerializer_work_with_List_that_must_be_settable()
        {
            var o = new IndependentObject() { Name = "Doe", Status = Status.Two };
            o.Sub = new List<SubObject>() { new SubObject() { Name = "S", X = 37, Y = 12 } };

            byte[] bin;
            using( var m = new MemoryStream() )
            using( var w = new Utf8JsonWriter( m ) )
            {
                w.WriteStartObject();
                w.WriteBoolean( "working", true );
                w.WritePropertyName( "obj" );
                JsonSerializer.Serialize( w, o );
                w.WriteBoolean( "worked", true );
                w.WriteEndObject();
                w.Flush();
                bin = m.ToArray();
                var binText = Encoding.UTF8.GetString( m.ToArray() );
                binText.Should().Be( @"{""working"":true,""obj"":{""Status"":2,""Name"":""Doe"",""Sub"":[{""X"":37,""Y"":12,""Name"":""S""}]},""worked"":true}" );
            }
            var r = new Utf8JsonReader( bin );
            r.Read(); // TokenType = None
            r.Read(); // StartObject
            r.GetString().Should().Be( "working" );
            r.Read(); // PropertyName
            r.GetBoolean().Should().BeTrue();
            r.Read();
            r.Read(); // PropertyName
            var o2 = JsonSerializer.Deserialize<IndependentObject>( ref r );
            r.TokenType.Should().Be( JsonTokenType.EndObject );
            r.Read();

            r.GetString().Should().Be( "worked" );
            r.Read(); // PropertyName
            r.GetBoolean().Should().BeTrue();
            r.Read();
            r.TokenType.Should().Be( JsonTokenType.EndObject );
            r.Read();
            r.Read().Should().BeFalse();

            o2.Should().BeEquivalentTo( o );
        }


        public interface ITest : IPoco
        {
            IndependentObject Obj { get; set; }
        }


        [Test]
        public void simple_poco_serialization_using_the_JsonSerializer()
        {
            var oI = new IndependentObject() { Name = "Doe", Status = Status.Two };
            oI.Sub = new List<SubObject>() { new SubObject() { Name = "S", X = 37, Y = 12 } };

            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Obj = oI; } );
            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Should().BeEquivalentTo( o );
        }

    }
}
