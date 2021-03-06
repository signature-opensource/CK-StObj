using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class PocoJsonTests
    {
        public interface ITest : IPoco
        {
            int Power { get; set; }

            [DefaultValue( "Hello..." )]
            string Hip { get; set; }
        }

        [Test]
        public void null_poco_is_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            ITest? nullPoco = null;

            ITest? o2 = Roundtrip( s, nullPoco );
            o2.Should().BeNull();

            IPoco? nullUnknwonPoco = null;

            IPoco? o3 = Roundtrip( s, nullUnknwonPoco );
            o3.Should().BeNull();
        }

        [Test]
        public void simple_poco_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Power = 3712; o.Hip += "CodeGen!"; } );
            var o2 = Roundtrip( s, o );

            Debug.Assert( o2 != null );
            o2.Power.Should().Be( o.Power );
            o2.Hip.Should().Be( o.Hip );
        }

        [Test]
        public void poco_ToString_overridden_method_returns_its_Json_representation()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Power = 3712; o.Hip = "Here!"; } );
            o.ToString().Should().Be( @"{""Power"":3712,""Hip"":""Here!""}" );
        }

        public interface IPocoA : IPoco
        {
            [DefaultValue( "A" )]
            string ValA { get; set; }

            IPocoB B { get; }

            IPocoB B2 { get; }
        }

        public interface IPocoB : IPoco
        {
            [DefaultValue( "B" )]
            string ValB { get; set; }

            IPocoC C { get; }
        }

        public interface IPocoC : IPoco
        {
            [DefaultValue( "C" )]
            string ValC { get; set; }
        }


        [Test]
        public void property_poco_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoA ), typeof( IPocoB ), typeof( IPocoC ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var fA = s.GetRequiredService<IPocoFactory<IPocoA>>();
            var a = fA.Create( a => { a.B2.ValB = "B2"; a.B2.C.ValC = "B2.C"; } );

            var a2 = Roundtrip( s, a );
            Debug.Assert( a2 != null );
            a2.B2.ValB.Should().Be( "B2" );
            a2.B2.C.ValC.Should().Be( "B2.C" );
            a2.Should().BeEquivalentTo( a );
        }

        public interface IPocoWithGeneric : IPoco
        {
            IPoco? OnePoco { get; set; }
            IPoco? AnotherOnePoco { get; set; }
        }

        [Test]
        public void generic_property_poco_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithGeneric ), typeof( IPocoA ), typeof( IPocoB ), typeof( IPocoC ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var fG = s.GetRequiredService<IPocoFactory<IPocoWithGeneric>>();
            var fA = s.GetRequiredService<IPocoFactory<IPocoA>>();

            var g = fG.Create();

            var gWithNull = Roundtrip( s, g );
            Debug.Assert( gWithNull != null );
            gWithNull.OnePoco.Should().BeNull();
            gWithNull.AnotherOnePoco.Should().BeNull();

            g.OnePoco = fA.Create();
            var gWithA = Roundtrip( s, g );
            Debug.Assert( gWithA != null );
            gWithA.OnePoco.Should().NotBeNull();
            gWithA.AnotherOnePoco.Should().BeNull();
        }



        [Test]
        public void recursive_poco_properties_throw_InvalidOperationException_by_JsonWriter()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithGeneric ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var fG = s.GetRequiredService<IPocoFactory<IPocoWithGeneric>>();

            var g1 = fG.Create();
            var g2 = fG.Create();
            var g3 = fG.Create();

            g1.OnePoco = g2;
            g2.OnePoco = g3;

            var gCool = Roundtrip( s, g1 );

            g2.AnotherOnePoco = g3;
            var gDup = Roundtrip( s, g2 );
            Debug.Assert( gDup != null );
            gDup.AnotherOnePoco.Should().NotBeNull().And.NotBeSameAs( gDup.OnePoco );
            gDup.OnePoco.Should().NotBeNull();

            g2.AnotherOnePoco = g1;
            g1.Invoking( fail => Roundtrip( s, fail ) ).Should().Throw<InvalidOperationException>();

            g2.AnotherOnePoco = g2;
            g1.Invoking( fail => Roundtrip( s, fail ) ).Should().Throw<InvalidOperationException>();

        }

        public interface IPocoWithBasicNullableList : IPoco
        {
            IList<int>? Values { get; }

            ISet<DateTime>? NullableCollection { get; set; }
        }

        [Test]
        public void basic_lists_can_be_nullable()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithBasicNullableList ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<IPocoWithBasicNullableList>>();

            var lA = f.Create();
            Debug.Assert( lA.Values != null );
            lA.Values.Add( -12 );
            lA.Values.Add( 3712 );
            lA.NullableCollection.Should().BeNull();

            var lA2 = Roundtrip( s, lA );
            Debug.Assert( lA2 != null );
            lA2.Should().BeEquivalentTo( lA );

            lA.NullableCollection = new HashSet<DateTime>();
            lA.NullableCollection.Add( DateTime.UtcNow );

            var lA3 = Roundtrip( s, lA );
            Debug.Assert( lA3 != null );
            lA3.Should().BeEquivalentTo( lA );
        }

        public interface IPocoWithBasicList2 : IPocoWithBasicNullableList
        {
            new ISet<DateTime>? NullableCollection { get; }
        }

        [Test]
        public void properties_can_be_nullable_but_AutoImplemented_as_long_as_one_interface_requires_it()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithBasicNullableList ), typeof( IPocoWithBasicList2 ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<IPocoWithBasicNullableList>>();
            var lA = f.Create();
            Debug.Assert( lA.Values != null );
            lA.Values.Add( -12 );
            lA.Values.Add( 3712 );
            lA.NullableCollection.Should().NotBeNull( "IPocoWithBasicList2 has no setter: it is AutoImplemented." );

            var lA2 = Roundtrip( s, lA );
            Debug.Assert( lA2 != null );
            lA2.Should().BeEquivalentTo( lA );

            lA.NullableCollection = null!;

            var lA3 = Roundtrip( s, lA );
            Debug.Assert( lA3 != null );
            lA3.Should().BeEquivalentTo( lA );
        }

        public interface ITestSetNumbers : ITest
        {
            HashSet<int> Numbers { get; }
        }

        [Test]
        public void Set_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITestSetNumbers ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<ITestSetNumbers>>();
            var o = f.Create( o =>
            {
                o.Power = 3712;
                o.Hip += "CodeGen!";
                o.Numbers.AddRangeArray( 12, 87, 12, 54, 12 );
            } );
            var o2 = Roundtrip( s, o );
            Debug.Assert( o2 != null );
            o2.Power.Should().Be( o.Power );
            o2.Hip.Should().Be( o.Hip );
            o2.Numbers.Should().BeEquivalentTo( o.Numbers );
        }


        public interface IWithSet : IPoco
        {
            ISet<int> Numbers { get; }
        }

        public interface IWithList : IPoco
        {
            List<int> Numbers { get; }
        }

        [Test]
        public void List_and_Set_can_read_each_other()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithSet ), typeof( IWithList ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            var fSet = services.GetRequiredService<IPocoFactory<IWithSet>>();
            var fList = services.GetRequiredService<IPocoFactory<IWithList>>();
            var oS = fSet.Create( o =>
            {
                o.Numbers.AddRangeArray( 12, 87, 12, 54, 12 );
            } );
            var oL = fList.Create( o =>
            {
                o.Numbers.AddRangeArray( 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 );
            } );
            var oSb = Serialize( oS, false );
            var oLb = Serialize( oL, false );
            var oLFromS = Deserialize<IWithList>( services, oSb.Span );
            var oSFromL = Deserialize<IWithSet>( services, oLb.Span );
            Debug.Assert( oLFromS != null && oSFromL != null );

            oLFromS.Numbers.Should().BeEquivalentTo( 12, 87, 54 );
            oSFromL.Numbers.Should().BeEquivalentTo( 1, 2, 3, 4, 5 );
        }


        [Test]
        public void missing_and_extra_properties_in_Json_are_ignored_Missing_have_their_DefaultValue_Extra_are_skipped()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            string missingValue = @"{""Hip"": ""Hop"", ""Stranger"": [0,1,[]]}";
            var noValue = Deserialize<ITest>( s, missingValue );
            Debug.Assert( noValue != null );
            noValue.Hip.Should().Be( "Hop" );
            noValue.Power.Should().Be( 0 );

            string missingHip = @"{""Power"": 871871, ""Another"": {""Nimp"": [87,54]}, ""Stranger"": []}";
            var noHip = Deserialize<ITest>( s, missingHip );
            Debug.Assert( noHip != null );
            noHip.Hip.Should().Be( "Hello...", "This is the default Hip value." );
            noHip.Power.Should().Be( 871871 );
        }

        public interface IPocoCrossA : IPoco
        {
            IPocoCrossB B { get; }

            string MsgA { get; set; }
        }

        public interface IPocoCrossB : IPoco
        {
            IPocoCrossA? A { get; set; }

            string MsgB { get; set; }
        }

        [Test]
        public void cross_poco_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoCrossA ), typeof( IPocoCrossB ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            var fA = services.GetRequiredService<IPocoFactory<IPocoCrossA>>();
            var a = fA.Create( a =>
            {
                a.B.MsgB = "From A.B.";
            } );

            var a2 = Roundtrip( services, a );
            Debug.Assert( a2 != null );
            a2.B.MsgB.Should().Be( "From A.B." );

            a.B.A = a;
            a.Invoking( fail => Roundtrip( services, fail ) ).Should().Throw<InvalidOperationException>();
        }

        public interface IPocoWithDictionary : IPoco
        {
            IDictionary<string, int> ClassicalJson { get; }

            IDictionary<int, string> Map { get; }
        }

        [Test]
        public void dictionary_with_a_string_key_is_a_Json_object_otherwise_it_is_an_array_of_2_cells_array()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithDictionary ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            var fA = services.GetRequiredService<IPocoFactory<IPocoWithDictionary>>();
            var a = fA.Create( a =>
            {
                a.Map.Add( 1, "Toto" );
                a.ClassicalJson.Add( "key", 3712 );
            } );
            var a2 = Roundtrip( services, a );

            a2.Should().BeEquivalentTo( a );
        }

        public interface IPocoWithObject : IPoco
        {
            object Value { get; set; }
        }

        [Test]
        public void basic_types_properties_as_Object_are_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            var f = services.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var a = f.Create( a =>
            {
                a.Value = 1;
            } );
            var a2 = Roundtrip( services, a );
            a2.Should().BeEquivalentTo( a );
        }

        [Test]
        public void IPoco_types_properties_as_Object_are_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ), typeof( IPocoWithDictionary ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            var f = services.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var fO = services.GetRequiredService<IPocoFactory<IPocoWithDictionary>>();
            var o = fO.Create( o =>
            {
                o.ClassicalJson.Add( "Name", 712 );
                o.Map[3712] = "Hop";
                o.Map[0] = "Zero";
            } );
            var a = f.Create( a =>
            {
                a.Value = o;
            } );
            var a2 = Roundtrip( services, a );
            a2.Should().BeEquivalentTo( a );
        }

        [Test]
        public void IPoco_types_properties_as_known_Collections_are_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ), typeof( IPocoWithDictionary ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            // IPocoWithDictionary brings Dictionary<string, int> and Dictionary<int,string>.
            var f = services.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var a = f.Create( a =>
            {
                a.Value = new Dictionary<int, string>() { { 1, "One" }, { 2, "Two" }, { 3, "Three" } };
            } );
            var a2 = Roundtrip( services, a );
            a2.Should().BeEquivalentTo( a );

            a.Value = new Dictionary<string, int>() { { "One", 1 }, { "Two", 2 }, { "Three", 3 } };
            Roundtrip( services, a ).Should().BeEquivalentTo( a );
        }

        [Test]
        public void IPoco_types_properties_must_be_known()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoCrossA ) );
                TestHelper.GenerateCode( c ).CodeGen.Success.Should().BeFalse();
            }
            {
                // Here we don't add IPocoWithDictionary: Dictionary<string, int> and Dictionary<int,string> are not registered.
                var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ) );
                var services = TestHelper.GetAutomaticServices( c ).Services;
                var f = services.GetRequiredService<IPocoFactory<IPocoWithObject>>();
                var a = f.Create( a =>
                {
                    a.Value = new Dictionary<int, string>() { { 1, "One" }, { 2, "Two" }, { 3, "Three" } };
                } );
                a.Invoking( x => Roundtrip( services, x ) ).Should().Throw<JsonException>();
            }
        }

        [ExternalName( "WithUnionType" )]
        public interface IWithUnionType : IPoco
        {
            [UnionType( typeof(IList<int>), typeof(int), typeof(IDictionary<int, string?>) )]
            object V { get; set; }
        }

        [Test]
        public void UnionTypes_automatically_register_the_allowed_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithUnionType ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var f = services.GetRequiredService<IPocoFactory<IWithUnionType>>();
            var a = f.Create( a =>
            {
                a.V = new Dictionary<int, string?>() { { 1, "One" }, { 2, "Two" }, { 3, "Three" }, { 3712, null } };
            } );
            Roundtrip( services, a );

            a.V = 56;
            Roundtrip( services, a );

            a.V = new List<int>() { 45, 87, 87, 254, 87 };
            Roundtrip( services, a );

            // UnionType restricts the type but allows null.
            a.V = null!;
            Roundtrip( services, a );

            a.Invoking( x => x.V = "lj" ).Should().Throw<ArgumentException>();
        }


        static ReadOnlyMemory<byte> Serialize( IPoco o, bool withType )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                o.Write( w, withType );
                w.Flush();
            }
            return m.WrittenMemory;
        }

        public static T? Deserialize<T>( IServiceProvider services, ReadOnlySpan<byte> b ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( b );
            var f = services.GetRequiredService<IPocoFactory<T>>();
            return f.Read( ref r );
        }

        public static T? Deserialize<T>( IServiceProvider services, string s ) where T : class, IPoco
        {
            return Deserialize<T>( services, Encoding.UTF8.GetBytes( s ) );
        }

        public static T? Roundtrip<T>( IServiceProvider services, T? o ) where T : class, IPoco
        {
            byte[] bin1;
            string bin1Text;
            var directory = services.GetService<PocoDirectory>();
            using( var m = new MemoryStream() )
            {
                try
                {
                    using( var w = new Utf8JsonWriter( m ) )
                    {
                        o.Write( w, true );
                        w.Flush();
                    }
                    bin1 = m.ToArray();
                    bin1Text = Encoding.UTF8.GetString( bin1 );
                }
                catch( Exception )
                {
                    // On error, bin1 and bin1Text can be inspected here.
                    throw;
                }

                var r1 = new Utf8JsonReader( bin1 );

                var o2 = directory.ReadPocoValue( ref r1 );

                m.Position = 0;
                using( var w2 = new Utf8JsonWriter( m ) )
                {
                    o2.Write( w2, true );
                    w2.Flush();
                }
                var bin2 = m.ToArray();

                bin1.Should().BeEquivalentTo( bin2 );

                // Is this an actual Poco or a definer?
                // When it's a definer, there is no factory!
                var f = services.GetService<IPocoFactory<T>>();
                if( f != null )
                {
                    var r2 = new Utf8JsonReader( bin2 );
                    var o3 = f.Read( ref r2 );
                    return o3;
                }
                return (T?)o2;
            }

        }


    }
}
