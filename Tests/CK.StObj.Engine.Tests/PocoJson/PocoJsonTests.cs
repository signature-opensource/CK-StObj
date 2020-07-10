using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
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

        public interface IPocoA : IPoco
        {
            [DefaultValue("A")]
            string ValA { get; set; }

            IPocoB B { get; }

            IPocoB B2 { get; }
        }

        public interface IPocoB : IPoco
        {
            [DefaultValue("B")]
            string ValB { get; set; }

            IPocoC C { get; }
        }

        public interface IPocoC : IPoco
        {
            [DefaultValue("C")]
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
            IPoco OnePoco { get; set; }
            IPoco AnotherOnePoco { get; set; }
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

        public interface IPocoWithBasicList : IPoco
        {
            IList<int> Values { get; }

            ISet<DateTime> NullableCollection { get; set; }
        }

        [Test]
        public void basic_lists_can_be_nullable()
        {
            //var gen = new GeneratedRootContext( TestHelper.Monitor, StObjContextRoot.BasicStObjRuntimeBuilder );
            //var s = new ServiceCollection().AddStObjMap( TestHelper.Monitor, gen ).BuildServiceProvider();

            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithBasicList ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<IPocoWithBasicList>>();

            var lA = f.Create();
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

        public interface IPocoWithBasicList2 : IPocoWithBasicList
        {
            new ISet<DateTime> NullableCollection { get; }
        }

        [Test]
        public void properties_can_be_nullable_but_AutoImplemented_as_long_as_one_interface_requires_it()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithBasicList ), typeof( IPocoWithBasicList2 ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<IPocoWithBasicList>>();
            var lA = f.Create();
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
            var oLFromS = Deserialize<IWithList>( services, oSb );
            var oSFromL = Deserialize<IWithSet>( services, oLb );
            Debug.Assert( oLFromS != null && oSFromL != null );

            oLFromS.Numbers.Should().BeEquivalentTo( 12, 87, 54 );
            oSFromL.Numbers.Should().BeEquivalentTo( 1, 2, 3, 4, 5 );
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


        static byte[] Serialize( IPoco o, bool withType )
        {
            var m = new MemoryStream();
            using( var w = new Utf8JsonWriter( m ) )
            {
                o.Write( w, withType );
                w.Flush();
            }
            return m.ToArray();
        }

        static T? Deserialize<T>( IServiceProvider services, byte[] b ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( b );
            var f = services.GetRequiredService<IPocoFactory<T>>();
            return f.Read( ref r );
        }

        static T? Roundtrip<T>( IServiceProvider services, T? o ) where T : class, IPoco
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
