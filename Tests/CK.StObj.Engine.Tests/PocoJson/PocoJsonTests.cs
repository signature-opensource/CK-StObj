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
            var directory = s.GetService<PocoDirectory>();

            ITest? nullPoco = null;

            JsonTestHelper.Roundtrip( directory, nullPoco ).Should().BeNull();

            IPoco? nullUnknwonPoco = null;

            JsonTestHelper.Roundtrip( directory, nullUnknwonPoco ).Should().BeNull();
        }

        [Test]
        public void simple_poco_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Power = 3712; o.Hip += "CodeGen!"; } );
            var o2 = JsonTestHelper.Roundtrip( directory, o );
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
            var directory = s.GetService<PocoDirectory>();

            var fA = s.GetRequiredService<IPocoFactory<IPocoA>>();
            var a = fA.Create( a => { a.B2.ValB = "B2"; a.B2.C.ValC = "B2.C"; } );

            var a2 = JsonTestHelper.Roundtrip( directory, a );
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
            var directory = s.GetService<PocoDirectory>();

            var fG = s.GetRequiredService<IPocoFactory<IPocoWithGeneric>>();
            var fA = s.GetRequiredService<IPocoFactory<IPocoA>>();

            var g = fG.Create();

            var gWithNull = JsonTestHelper.Roundtrip( directory, g );
            gWithNull.OnePoco.Should().BeNull();
            gWithNull.AnotherOnePoco.Should().BeNull();

            g.OnePoco = fA.Create();
            var gWithA = JsonTestHelper.Roundtrip( directory, g );
            gWithA.OnePoco.Should().NotBeNull();
            gWithA.AnotherOnePoco.Should().BeNull();
        }



        [Test]
        public void recursive_poco_properties_throw_InvalidOperationException_by_JsonWriter()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithGeneric ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var fG = s.GetRequiredService<IPocoFactory<IPocoWithGeneric>>();

            var g1 = fG.Create();
            var g2 = fG.Create();
            var g3 = fG.Create();

            g1.OnePoco = g2;
            g2.OnePoco = g3;

            var gCool = JsonTestHelper.Roundtrip( directory, g1 );

            g2.AnotherOnePoco = g3;
            var gDup = JsonTestHelper.Roundtrip( directory, g2 );
            gDup.AnotherOnePoco.Should().NotBeNull().And.NotBeSameAs( gDup.OnePoco );
            gDup.OnePoco.Should().NotBeNull();

            g2.AnotherOnePoco = g1;
            g1.Invoking( fail => JsonTestHelper.Roundtrip( directory, fail ) ).Should().Throw<InvalidOperationException>();

            g2.AnotherOnePoco = g2;
            g1.Invoking( fail => JsonTestHelper.Roundtrip( directory, fail ) ).Should().Throw<InvalidOperationException>();

        }

        public interface ITestSetNumbers : ITest
        {
            ISet<decimal> Numbers { get; }
        }

        [TestCase( PocoJsonSerializerMode.ECMAScriptSafe )]
        [TestCase( PocoJsonSerializerMode.ECMAScriptStandard )]
        public void Set_serialization( PocoJsonSerializerMode mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITestSetNumbers ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<ITestSetNumbers>>();
            var o = f.Create( o =>
            {
                o.Power = 3712;
                o.Hip += "CodeGen!";
                o.Numbers.AddRangeArray( 12, 87, 12, 54, 12 );
            } );
            var o2 = JsonTestHelper.Roundtrip( directory, o, new PocoJsonSerializerOptions { Mode = mode }, text: t => TestHelper.Monitor.Info( t ) );
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
            IList<int> Numbers { get; }
        }

        [Test]
        public void ISet_and_IList_can_read_each_other()
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
            var oSb = oS.JsonSerialize( false );
            var oLb = oL.JsonSerialize( false );
            var oLFromS = JsonTestHelper.Deserialize<IWithList>( services, oSb.Span );
            var oSFromL = JsonTestHelper.Deserialize<IWithSet>( services, oLb.Span );
            Debug.Assert( oLFromS != null && oSFromL != null );

            oLFromS.Numbers.Should().BeEquivalentTo( 12, 87, 54 );
            oSFromL.Numbers.Should().BeEquivalentTo( 1, 2, 3, 4, 5 );
        }

        public interface IWithArray : IPoco
        {
            int[] Numbers { get; set; }
        }

        [Test]
        public void ISet_and_Array_can_read_each_other()
        {
            // Implementation uses this fact: an array is a IList.
            typeof( int[] ).Should().BeAssignableTo<IList<int>>();

            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithArray ), typeof( IWithSet ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            var fSet = services.GetRequiredService<IPocoFactory<IWithSet>>();
            var fArray = services.GetRequiredService<IPocoFactory<IWithArray>>();
            var oS = fSet.Create( o =>
            {
                o.Numbers.AddRangeArray( 12, 87, 12, 54, 12 );
            } );
            var oA = fArray.Create( o =>
            {
                o.Numbers = new int[] { 1, 2, 3, 4, 5, 1, 2, 3, 4, 5 };
            } );
            var oSb = oS.JsonSerialize( false );
            var oAb = oA.JsonSerialize( false );
            var oAFromS = JsonTestHelper.Deserialize<IWithArray>( services, oSb.Span );
            var oSFromA = JsonTestHelper.Deserialize<IWithSet>( services, oAb.Span );
            Debug.Assert( oAFromS != null && oSFromA != null );

            oAFromS.Numbers.Should().BeEquivalentTo( 12, 87, 54 );
            oSFromA.Numbers.Should().BeEquivalentTo( 1, 2, 3, 4, 5 );
        }


        [Test]
        public void missing_and_extra_properties_in_Json_are_ignored_Missing_have_their_DefaultValue_Extra_are_skipped()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            string missingValue = @"{""Hip"": ""Hop"", ""Stranger"": [0,1,[]]}";
            var noValue = JsonTestHelper.Deserialize<ITest>( s, missingValue );
            Debug.Assert( noValue != null );
            noValue.Hip.Should().Be( "Hop" );
            noValue.Power.Should().Be( 0 );

            string missingHip = @"{""Power"": 871871, ""Another"": {""Nimp"": [87,54]}, ""Stranger"": []}";
            var noHip = JsonTestHelper.Deserialize<ITest>( s, missingHip );
            Debug.Assert( noHip != null );
            noHip.Hip.Should().Be( "Hello...", "This is the default Hip value." );
            noHip.Power.Should().Be( 871871 );
        }

        public interface IPocoCrossA : IPoco
        {
            IPocoCrossB B { get; }

            [DefaultValue( "A message." )]
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
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var fA = s.GetRequiredService<IPocoFactory<IPocoCrossA>>();
            var a = fA.Create( a =>
            {
                a.B.MsgB = "From A.B.";
            } );

            var a2 = JsonTestHelper.Roundtrip( directory, a );
            Debug.Assert( a2 != null );
            a2.B.MsgB.Should().Be( "From A.B." );

            a.B.A = a;
            a.Invoking( fail => JsonTestHelper.Roundtrip( directory, fail ) ).Should().Throw<InvalidOperationException>();
        }

        public interface IPocoCrossASpec : IPocoCrossA
        {
            [DefaultValue( "A specialized message." )]
            string MsgASpec { get; set; }
        }


        [Test]
        public void cross_poco_serialization_with_specialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoCrossASpec ), typeof( IPocoCrossB ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var fA = s.GetRequiredService<IPocoFactory<IPocoCrossA>>();
            var a = fA.Create( a =>
            {
                a.B.MsgB = "From A.B.";
            } );

            var a2 = JsonTestHelper.Roundtrip( directory, a );
            Debug.Assert( a2 != null );
            a2.B.MsgB.Should().Be( "From A.B." );

            a.B.A = a;
            a.Invoking( fail => JsonTestHelper.Roundtrip( directory, fail ) ).Should().Throw<InvalidOperationException>();
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
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var fA = s.GetRequiredService<IPocoFactory<IPocoWithDictionary>>();
            var a = fA.Create( a =>
            {
                a.Map.Add( 1, "Toto" );
                a.ClassicalJson.Add( "key", 3712 );
            } );
            var a2 = JsonTestHelper.Roundtrip( directory, a );

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
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var a = f.Create( a =>
            {
                a.Value = 1;
            } );
            var a2 = JsonTestHelper.Roundtrip( directory, a );
            a2.Should().BeEquivalentTo( a );
        }

        [Test]
        public void IPoco_types_properties_as_Object_are_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ), typeof( IPocoWithDictionary ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var fO = s.GetRequiredService<IPocoFactory<IPocoWithDictionary>>();
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
            var a2 = JsonTestHelper.Roundtrip( directory, a );
            a2.Should().BeEquivalentTo( a );
        }

        [Test]
        public void IPoco_types_properties_as_known_Collections_are_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ), typeof( IPocoWithDictionary ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            // IPocoWithDictionary brings Dictionary<string, int> and Dictionary<int,string>.
            var f = s.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var a = f.Create( a =>
            {
                a.Value = new Dictionary<int, string>() { { 1, "One" }, { 2, "Two" }, { 3, "Three" } };
            } );
            var a2 = JsonTestHelper.Roundtrip( directory, a );
            a2.Should().BeEquivalentTo( a );

            a.Value = new Dictionary<string, int>() { { "One", 1 }, { "Two", 2 }, { "Three", 3 } };
            JsonTestHelper.Roundtrip( directory, a ).Should().BeEquivalentTo( a );
        }

        [Test]
        public void IPoco_types_properties_must_be_known()
        {
            // Here we don't add IPocoWithDictionary: Dictionary<string, int> and Dictionary<int,string> are not registered.
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IPocoWithObject ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();
            var f = s.GetRequiredService<IPocoFactory<IPocoWithObject>>();
            var a = f.Create( a =>
            {
                a.Value = new Dictionary<int, string>() { { 1, "One" }, { 2, "Two" }, { 3, "Three" } };
            } );
            a.Invoking( x => JsonTestHelper.Roundtrip( directory, x ) ).Should().Throw<JsonException>();
        }

        [ExternalName( "WithUnionType" )]
        public interface IWithUnionType : IPoco
        {
            [UnionType]
            object? V { get; set; }

            class UnionTypes
            {
                public (IList<int>, int?, IDictionary<int, string>) V { get; }
            }
        }

        [Test]
        public void UnionTypes_automatically_register_the_allowed_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithUnionType ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();
            var f = services.GetRequiredService<IPocoFactory<IWithUnionType>>();
            var a = f.Create( a =>
            {
                a.V = new Dictionary<int, string?>() { { 1, "One" }, { 2, "Two" }, { 3, "Three" } };
            } );
            JsonTestHelper.Roundtrip( directory, a, text: t => TestHelper.Monitor.Info( t ) );

            a.V = 56;
            JsonTestHelper.Roundtrip( directory, a );

            a.V = new List<int>() { 45, 87, 87, 254, 87 };
            JsonTestHelper.Roundtrip( directory, a );

            // Null is allowed here (because of the nullable int).
            a.V = null!;
            JsonTestHelper.Roundtrip( directory, a );

            a.Invoking( x => x.V = "lj" ).Should().Throw<ArgumentException>( "String is not allowed." );
        }

        [Test]
        public void writing_UnionTypes_throws_on_nullability_violation_in_generics()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithUnionType ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();
            var f = services.GetRequiredService<IPocoFactory<IWithUnionType>>();
            var a = f.Create( a =>
            {
                a.V = new Dictionary<int, string>() { { 1, "One" }, { 3712, null! } };
            } );
            a.Invoking( _ => _.JsonSerialize() ).Should().Throw<InvalidOperationException>().WithMessage( "A null value appear where it should not.*" );
        }


    }
}
