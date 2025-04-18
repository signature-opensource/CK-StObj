using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class RecordWithReadOnlyCompliantTypeTests
{
    public interface IWithRecordStruct : IPoco
    {
        public record struct ThingDetail( int Power, string Name = "Albert" );

        ref ThingDetail? Thing1 { get; }
        ref ThingDetail Thing2 { get; }
    }

    [Test]
    public async Task record_struct_is_supported_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IWithRecordStruct ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var p = auto.Services.GetRequiredService<IPocoFactory<IWithRecordStruct>>().Create();
        p.Thing1.ShouldBeNull();
        p.Thing2.Name.ShouldNotBeNull();
        p.Thing2.Name.ShouldBe( "Albert" );
    }

    public struct DetailWithFields : IEquatable<DetailWithFields>
    {
        [DefaultValue( 42 )]
        public int Power;

        public List<int> Values;

        [DefaultValue( "Hip!" )]
        public string Name;

        public readonly bool Equals( DetailWithFields other ) => Power == other.Power
                                                                 && Name == other.Name
                                                                 && EqualityComparer<List<int>>.Default.Equals( Values, other.Values );

        public override bool Equals( [NotNullWhen( true )] object? obj ) => obj is DetailWithFields other && Equals( other );

        public override int GetHashCode()
        {
            return HashCode.Combine( Power, Name, EqualityComparer<List<int>>.Default.GetHashCode( Values ) );
        }
    }

    public struct DetailWithProperties : IEquatable<DetailWithProperties>
    {
        [DefaultValue( 3712 )]
        public int Power { get; set; }

        public List<int>? Values;

        [DefaultValue( "Hop!" )]
        public string? Name { get; set; }

        public readonly bool Equals( DetailWithProperties other ) => Power == other.Power
                                                                     && Name == other.Name
                                                                     && EqualityComparer<List<int>>.Default.Equals( Values, other.Values );

        public override bool Equals( [NotNullWhen( true )] object? obj ) => obj is DetailWithProperties other && Equals( other );

        public override int GetHashCode()
        {
            return HashCode.Combine( Power, Name, EqualityComparer<List<int>>.Default.GetHashCode( Values! ) );
        }
    }

    public interface IFailedWithStruct1 : IPoco
    {
        ref DetailWithFields WithFields { get; }
    }

    public interface IFailedWithStruct2 : IPoco
    {
        ref DetailWithProperties WithProperties { get; }
    }

    [Test]
    public void mutable_struct_must_also_be_ReadOnlyCompliant_in_Poco_fields()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IFailedWithStruct2 )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct2.WithProperties':",
            "  List<int>? Values" );

        TestHelper.GetFailedCollectorResult( [typeof( IFailedWithStruct1 )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct1.WithFields':",
            "  List<int> Values" );
    }

    public DetailWithFields WithFields { get; }
    public DetailWithProperties WithProperties { get; }

    [Test]
    public void records_can_be_NOT_ReadOnlyCompliant_when_not_in_a_Poco_field()
    {
        var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );
        {
            IPocoType p = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( WithFields ) )! )!;
            var pRec = (IRecordPocoType)p;
            pRec.IsReadOnlyCompliant.ShouldBeFalse();
        }
        {
            IPocoType p = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( WithProperties ) )! )!;
            var pRec = (IRecordPocoType)p;
            pRec.IsReadOnlyCompliant.ShouldBeFalse();
        }
    }

    public interface IWithComplexRecords : IPoco
    {
        public record struct MyThingDetail( int Power = 42, string Name = "Einstein" );

        public record struct Funny( MyThingDetail FP, (string S, (IWithRecordStruct.ThingDetail P, MyThingDetail F) Inner) A );

        ref (MyThingDetail F, MyThingDetail P) A { get; }

        ref (Funny Funny, Funny? Funny2) B { get; }
    }

    [Test]
    public async Task nesting_typed_and_anonymous_record_is_possible_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IWithComplexRecords ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var p = auto.Services.GetRequiredService<IPocoFactory<IWithComplexRecords>>().Create();

        p.A.F.Power.ShouldBe( 42 );
        p.A.F.Name.ShouldBe( "Einstein" );
        p.A.P.Power.ShouldBe( 42 );
        p.A.P.Name.ShouldBe( "Einstein" );

        p.B.Funny.FP.Power.ShouldBe( 42 );
        p.B.Funny.FP.Name.ShouldBe( "Einstein" );
        p.B.Funny.A.Inner.P.Power.ShouldBe( 0 );
        p.B.Funny.A.Inner.P.Name.ShouldBe( "Albert" );
        p.B.Funny.A.Inner.F.Power.ShouldBe( 42 );
        p.B.Funny.A.Inner.F.Name.ShouldBe( "Einstein" );
    }

    public interface IRecordWithPoco : IPoco
    {
        public record struct Rec( int A, (IFailedWithStruct1 IAmHere, int B) Inside );
        ref Rec Pof { get; }
    }

    public interface IRecordWithPoco2 : IPoco
    {
        public record struct Rec( IFailedWithStruct2 IAmHere, int B );
        ref Rec Pof { get; }
    }

    [Test]
    public void no_IPoco_can_appear_in_named_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IRecordWithPoco ), typeof( IFailedWithStruct1 )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IRecordWithPoco.Pof':",
            "  in '(CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct1 IAmHere,int B) Inside':",
            "    CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct1 IAmHere" );

        TestHelper.GetFailedCollectorResult( [typeof( IRecordWithPoco2 ), typeof( IFailedWithStruct2 )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IRecordWithPoco2.Pof':",
            "  CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct2 IAmHere" );
    }

    public interface IHoldRecList : IPoco
    {
        public record struct Rec( List<Rec> R, int A );

        ref Rec P { get; }
    }

    [Test]
    public void no_list_can_appear_in_named_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IHoldRecList )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecList.P':",
            "List<CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecList.Rec> R" );
    }

    public interface IHoldRecArray : IPoco
    {
        public record struct Rec( Rec[] R, int A );

        ref Rec P { get; }
    }

    [Test]
    public void no_array_can_appear_in_named_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IHoldRecArray )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecArray.P':",
            "  CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecArray.Rec[] R" );
    }

    public interface IHoldRecDic : IPoco
    {
        public record struct Rec( Dictionary<int, Rec> R, int A );

        ref Rec P { get; }
    }

    [Test]
    public void no_dictionary_can_appear_in_named_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IHoldRecDic )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecDic.P':",
            "  Dictionary<int,CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecDic.Rec> R" );
    }

    public interface IAnonymousRecordWithPoco : IPoco
    {
        ref (IFailedWithStruct1 A, int B) Pof { get; }
    }

    public interface IAnonymousRecordWithPoco2 : IPoco
    {
        ref (int A, (IFailedWithStruct2 IAmHere, int B) Inside) Pof { get; }
    }

    [Test]
    public void no_IPoco_can_appear_in_anonymous_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IAnonymousRecordWithPoco ), typeof( IFailedWithStruct1 )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IAnonymousRecordWithPoco.Pof':",
            "  CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct1 A" );

        TestHelper.GetFailedCollectorResult( [typeof( IAnonymousRecordWithPoco2 ), typeof( IFailedWithStruct2 )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IAnonymousRecordWithPoco2.Pof':",
            "  in '(CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct2 IAmHere,int B) Inside':",
            "    CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IFailedWithStruct2 IAmHere" );
    }

    public interface IHoldAnonymousRecList : IPoco
    {
        ref (List<int> R, int A) P { get; }
    }

    [Test]
    public void no_list_can_appear_in_anonymous_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IHoldAnonymousRecList )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldAnonymousRecList.P':",
            "List<int> R" );
    }

    public interface IHoldAnonymousRecArray : IPoco
    {
        ref ((int[] R, bool B) Inside, int A) P { get; }
    }

    [Test]
    public void no_array_can_appear_in_anonymous_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IHoldAnonymousRecArray )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldAnonymousRecArray.P':",
            "  in '(int[] R,bool B) Inside':",
            "    int[] R" );
    }

    public interface IHoldRecAnonymousDic : IPoco
    {
        ref (Dictionary<int, long> R, int A) P { get; }
    }

    [Test]
    public void no_dictionary_can_appear_in_anonymous_record()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IHoldRecAnonymousDic )],
            "Non read-only compliant types in 'CK.StObj.Engine.Tests.Poco.RecordWithReadOnlyCompliantTypeTests.IHoldRecAnonymousDic.P':",
            "  Dictionary<int,long> R" );
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
        TestHelper.GetFailedCollectorResult( [typeof( IWithGenericRecordStruct )], "Generic value type cannot be a Poco type" );
    }

    // Error CS8170  Struct members cannot return 'this' or other instance members by reference.
    //public struct ThisMayBetterButImpossible
    //{
    //    DetailWithFields _v;

    //    public ref DetailWithFields Thing => ref _v;
    //}

    // We cannot forbid this without preventing record struct positional parameter syntax
    // to work.
    public struct ValidSetterButNotIdeal : IEquatable<ValidSetterButNotIdeal>
    {
        public DetailWithFields Thing { get; set; }

        public bool Equals( ValidSetterButNotIdeal other )
        {
            return EqualityComparer<DetailWithFields>.Default.Equals( Thing, other.Thing );
        }
    }

    public ValidSetterButNotIdeal GetValidSetterButNotIdeal => default;

    // It's unfortunate that record struct positional parameter syntax generates
    // properties instead of fields. Simple fields (like in ValueTuple) are easier to use
    // with composite struct fields.
    public struct Simple : IEquatable<Simple>
    {
        public DetailWithFields Thing;

        public bool Equals( Simple other )
        {
            return EqualityComparer<DetailWithFields>.Default.Equals( Thing, other.Thing );
        }
    }

    public Simple GetSimple => default;

    [Test]
    public void ref_property_or_field_thats_the_question()
    {
        var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );
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
        defCode.ShouldBe( "new(){Thing = new(){Power = 42, Values = new List<int>(), Name = @\"Hip!\"}}" );
        t2.DefaultValueInfo.DefaultValue.ValueCSharpSource.ShouldBe( defCode );
    }


}
