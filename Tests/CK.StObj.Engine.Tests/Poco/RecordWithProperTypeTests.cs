using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class RecordWithProperTypeTests
    {
        public interface IWithRecordStruct : IPoco
        {
            public record struct ThingDetail( int Power, string Name = "Albert" );

            ref ThingDetail? Thing1 { get; }
            ref ThingDetail Thing2 { get; }
        }

        [Test]
        public void record_struct_is_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithRecordStruct ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithRecordStruct>>().Create();
            p.Thing1.Should().BeNull();
            p.Thing2.Should().NotBeNull();
            p.Thing2.Name.Should().Be( "Albert" );
        }

        public struct DetailWithFields
        {
            [DefaultValue( 42 )]
            public int Power;

            public List<int> Values;

            [DefaultValue( "Hip!" )]
            public string Name;
        }

        public struct DetailWithProperties
        {
            [DefaultValue( 3712 )]
            public int Power { get; set; }

            [DefaultValue( "Hop!" )]
            public string Name { get; set; }
        }


        public interface IWithStruct : IPoco
        {
            ref DetailWithFields WithFields { get; }
            ref DetailWithProperties WithProperties { get; }
        }

        [Test]
        public void basic_mutable_struct_is_possible()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithStruct ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithStruct>>().Create();

            p.WithFields.Power.Should().Be( 42 );
            p.WithFields.Values.Should().NotBeNull().And.BeEmpty();
            p.WithFields.Name.Should().Be( "Hip!" );

            p.WithProperties.Power.Should().Be( 3712 );
            p.WithProperties.Name.Should().Be( "Hop!" );
        }

        public interface IWithComplexRecords : IPoco
        {
            public record struct Funny( DetailWithProperties FP, (string S, (DetailWithProperties P, DetailWithFields F) Inner ) A );

            ref (DetailWithFields F, DetailWithProperties P) A { get; }

            ref (Funny Funny, Funny? Funny2) B { get; }
        }

        [Test]
        public void nesting_typed_and_anonymous_record_is_possible()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithComplexRecords ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithComplexRecords>>().Create();

            p.A.F.Power.Should().Be( 42 );
            p.A.F.Values.Should().NotBeNull().And.BeEmpty();
            p.A.F.Name.Should().Be( "Hip!" );
            p.A.P.Power.Should().Be( 3712 );
            p.A.P.Name.Should().Be( "Hop!" );

            p.B.Funny.FP.Power.Should().Be( 3712 );
            p.B.Funny.FP.Name.Should().Be( "Hop!" );
            p.B.Funny.A.Inner.P.Power.Should().Be( 3712 );
            p.B.Funny.A.Inner.P.Name.Should().Be( "Hop!" );
            p.B.Funny.A.Inner.F.Power.Should().Be( 42 );
            p.B.Funny.A.Inner.F.Values.Should().NotBeNull().And.BeEmpty();
            p.B.Funny.A.Inner.F.Name.Should().Be( "Hip!" );
        }

        public interface IRecordWithPoco : IPoco
        {
            public record struct Rec( int A, (IWithStruct IAmHere, int B) Inside );
            ref Rec Pof { get; }
        }

        public interface IRecordWithPoco2 : IPoco
        {
            public record struct Rec( IWithStruct IAmHere, int B );
            ref Rec Pof { get; }
        }

        [Test]
        public void no_IPoco_can_appear_in_named_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IRecordWithPoco ), typeof( IWithStruct ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in 'CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IRecordWithPoco.Rec':" );

            c = TestHelper.CreateStObjCollector( typeof( IRecordWithPoco2 ), typeof( IWithStruct ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in 'CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IRecordWithPoco2.Rec':" );
        }


        public interface IHoldRecList : IPoco
        {
            public record struct Rec( List<Rec> R, int A );

            ref Rec P { get; }
        }

        [Test]
        public void no_list_can_appear_in_named_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecList ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in 'CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IHoldRecList.Rec':" );
        }

        public interface IHoldRecArray : IPoco
        {
            public record struct Rec( Rec[] R, int A );

            ref Rec P { get; }
        }

        [Test]
        public void no_array_can_appear_in_named_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecArray ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in 'CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IHoldRecArray.Rec':" );
        }

        public interface IHoldRecDic : IPoco
        {
            public record struct Rec( Dictionary<int, Rec> R, int A );

            ref Rec P { get; }
        }

        [Test]
        public void no_dictionary_can_appear_in_named_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecDic ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in 'CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IHoldRecDic.Rec':" );
        }

        public interface IAnonymousRecordWithPoco : IPoco
        {
            ref (IWithStruct A, int B) Pof { get; }
        }

        public interface IAnonymousRecordWithPoco2 : IPoco
        {
            ref (int A, (IWithStruct IAmHere, int B) Inside) Pof { get; }
        }

        [Test]
        public void no_IPoco_can_appear_in_anonymous_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAnonymousRecordWithPoco ), typeof( IWithStruct ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in '(CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IWithStruct A,int B)':" );

            c = TestHelper.CreateStObjCollector( typeof( IAnonymousRecordWithPoco2 ), typeof( IWithStruct ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in '(CK.StObj.Engine.Tests.Poco.RecordWithProperTypeTests.IWithStruct IAmHere,int B)'" );
        }

        public interface IHoldAnonymousRecList : IPoco
        {
            ref (List<int> R, int A) P { get; }
        }

        [Test]
        public void no_list_can_appear_in_anonymous_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldAnonymousRecList ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in '(List<int> R,int A)':" );
        }

        public interface IHoldAnonymousRecArray : IPoco
        {
            ref ((int[] R, bool B) Inside, int A) P { get; }
        }

        [Test]
        public void no_array_can_appear_in_anonymous_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldAnonymousRecArray ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in '(int[] R,bool B)':" );
        }

        public interface IHoldRecAnonymousDic : IPoco
        {
            ref (Dictionary<int, long> R, int A) P { get; }
        }

        [Test]
        public void no_dictionary_can_appear_in_anonymous_record()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecAnonymousDic ) );
            TestHelper.GetFailedResult( c, "Invalid mutable reference types in '(Dictionary<int,long> R,int A)':" );
        }



        // To be investigated... This is doable but honestly, do we need this?
        public interface IWithGenericRecordStruct : IPoco
        {
            public record struct ThingDetail<T>( int Power, T X, List<int> Values, string Name = "Albert" );

            ref ThingDetail<int>? Thing1 { get; }
            ref ThingDetail<string> Thing2 { get; }
        }

        [Test]
        public void generic_record_is_not_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithGenericRecordStruct ) );
            TestHelper.GetFailedResult( c, "Generic value type cannot be a Poco type" );
        }

        // Error CS8170  Struct members cannot return 'this' or other instance members by reference.
        //public struct ThisMayBetterButImpossible
        //{
        //    DetailWithFields _v;

        //    public ref DetailWithFields Thing => ref _v;
        //}

        // We cannot forbid this without preventing record struct positional parameter syntax
        // to work.
        public struct ValidSetterButNotIdeal
        {
            public DetailWithFields Thing { get; set; }
        }

        public ValidSetterButNotIdeal GetValidSetterButNotIdeal => default;

        // It's unfortunate that record struct positional parameter syntax generates
        // properties instead of fields. Simple fields (like in ValueTuple) are easier to use
        // with composite struct fields.
        public struct Simple
        {
            public DetailWithFields Thing;
        }

        public Simple GetSimple => default;

        [Test]
        public void ref_property_or_field_thats_the_question()
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );
            var t1 = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetValidSetterButNotIdeal ) )! );
            Debug.Assert( t1 != null );
            var t2 = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetSimple ) )! );
            Debug.Assert( t2 != null );

            // TBI: Why is Simple usable without initialization but ValidSetterButNotIdeal is not? (new() is required...)
            Simple sField;
            ValidSetterButNotIdeal sProp = new();

            sField.Thing.Power = 45;
            // You cannot do this.
            // sProp.Thing.Power = 45;
            Debug.Assert( sProp.Thing.Values == null, "It is the Poco framework that is able to correctly initialize properties." );

            // Both Requires Initialization.
            Debug.Assert( t1.DefaultValueInfo.RequiresInit );
            Debug.Assert( t2.DefaultValueInfo.RequiresInit );

            // The same initialization.
            var defCode = t1.DefaultValueInfo.DefaultValue.ValueCSharpSource;
            defCode.Should().Be( "new(){Thing = new(){Power = 42, Values = new List<int>(), Name = @\"Hip!\"}}" );
            t2.DefaultValueInfo.DefaultValue.ValueCSharpSource.Should().Be( defCode );
        }


    }
}