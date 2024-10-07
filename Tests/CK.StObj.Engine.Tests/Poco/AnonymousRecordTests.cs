using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class AnonymousRecordTests
{
    public interface IInvalidValueTupleSetter : IPoco
    {
        (int Count, string Name) Thing { get; set; }
    }

    [Test]
    public void writable_anonymous_record_must_be_a_ref_property()
    {
        TestHelper.GetFailedCollectorResult( [typeof( IInvalidValueTupleSetter )], "' must be a ref property: 'ref (int Count,string Name) Thing { get; }'." );
    }

    public interface IWithValueTuple : IPoco
    {
        ref (int Count, string NotNullableStringDefaultsToEmpty, DateTime DateTimeDefaultsToUtcMinValue) Thing { get; }
    }

    [Test]
    public void non_nullable_string_defaults_to_empty_and_DateTime_defaults_to_Util_UtcMinValue()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IWithValueTuple ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var p = auto.Services.GetRequiredService<IPocoFactory<IWithValueTuple>>().Create();

        p.Thing.Count.Should().Be( 0 );
        p.Thing.NotNullableStringDefaultsToEmpty.Should().BeEmpty();
        p.Thing.DateTimeDefaultsToUtcMinValue.Should().Be( Util.UtcMinValue );

        p.Thing.Count = 34;
        p.Thing.DateTimeDefaultsToUtcMinValue = Util.UtcMaxValue;

        p.Thing.Count.Should().Be( 34 );
        p.Thing.NotNullableStringDefaultsToEmpty.Should().BeEmpty();
        p.Thing.DateTimeDefaultsToUtcMinValue.Should().Be( Util.UtcMaxValue );
    }

    public interface IWithValueTuple2 : IPoco
    {
        ref (int, string, string?, Guid?) Power { get; }
    }

    [Test]
    public void nullables_are_let_to_null()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IWithValueTuple2 ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        var p = auto.Services.GetRequiredService<IPocoFactory<IWithValueTuple2>>().Create();

        p.Power.Item1.Should().Be( 0 );
        p.Power.Item2.Should().BeEmpty();
        p.Power.Item3.Should().BeNull();
        p.Power.Item4.Should().BeNull();
    }

    public interface IWithN : IPoco
    {
        ref (string? A, (string? S, int I) B, ((string? S1, string S2), string? S3)? C) Thing { get; }
    }

    [CKTypeDefiner]
    public interface IWithNotNPart0 : IPoco
    {
        ref (string? A, (string? S, int I) B, ((string? S1, string S2), string? S3) C) Thing { get; }
    }

    [CKTypeDefiner]
    public interface IWithNotNPart1 : IPoco
    {
        ref (string? A, (string? S, int I) B, ((string? S1, string S2), string S3)? C) Thing { get; }
    }

    [CKTypeDefiner]
    public interface IWithNotNPart2 : IPoco
    {
        ref (string? A, (string? S, int I) B, ((string? S1, string? S2), string? S3)? C) Thing { get; }
    }

    [CKTypeDefiner]
    public interface IWithNotNPart3 : IPoco
    {
        ref (string? A, (string? S, int I) B, ((string S1, string S2), string? S3)? C) Thing { get; }
    }

    [CKTypeDefiner]
    public interface IWithNotNPart4 : IPoco
    {
        ref (string? A, (string S, int I) B, ((string? S1, string S2), string? S3)? C) Thing { get; }
    }

    [CKTypeDefiner]
    public interface IWithNotNPart5 : IPoco
    {
        ref (string A, (string? S, int I) B, ((string? S1, string S2), string? S3)? C) Thing { get; }
    }


    // Thing is writable: it must have the same nullability.
    public interface INullabilityError0 : IWithN, IWithNotNPart0 { }
    public interface INullabilityError1 : IWithN, IWithNotNPart1 { }
    public interface INullabilityError2 : IWithN, IWithNotNPart2 { }
    public interface INullabilityError3 : IWithN, IWithNotNPart3 { }
    public interface INullabilityError4 : IWithN, IWithNotNPart4 { }
    public interface INullabilityError5 : IWithN, IWithNotNPart5 { }

    [CKTypeDefiner]
    public interface IWithNPart : IPoco
    {
        ref (string? A, (string? S, int I) B, ((string? S1, string S2), string? S3)? C) Thing { get; }
    }

    public interface INoError : IWithN, IWithNPart
    {
    }

    [Test]
    public void nullability_incoherence_is_checked()
    {
        TestHelper.GetSuccessfulCollectorResult( [typeof( INoError )] );

        CheckError( typeof( INullabilityError0 ) );
        CheckError( typeof( INullabilityError1 ) );
        CheckError( typeof( INullabilityError2 ) );
        CheckError( typeof( INullabilityError3 ) );
        CheckError( typeof( INullabilityError4 ) );
        CheckError( typeof( INullabilityError5 ) );

        static void CheckError( Type tError )
        {
            // Property type conflict between:
            // (string,(string,int),((string,string),string))& CK.StObj.Engine.Tests.Poco.AnonymousRecordTests.IWithNotNPart0.Thing
            // And:
            // (string,(string,int),((string,string),string)?)& CK.StObj.Engine.Tests.Poco.AnonymousRecordTests.IWithN.Thing
            TestHelper.GetFailedCollectorResult( [tError], "(string,(string,int),((string,string),string)?)& " );
        }
    }

    public interface IWithLongTuple : IPoco
    {
        ref (string F1,
              string F2,
              string F3,
              string F4,
              string F5,
              string F6,
              string F7,
              string F8,
              string F9,
              string F10,
              string F11,
              string F12,
              string F13,
              string F14,
              string F15,
              string F16,
              string F17,
              string F18,
              string F19,
              string F20
            ) Long
        { get; }
    }


    [Test]
    public void long_value_tuples_are_handled()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IWithLongTuple ) );
        var engineResult = configuration.Run();

        var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

        var tPoco = ts.FindByType<IPrimaryPocoType>( typeof( IWithLongTuple ) );
        Debug.Assert( tPoco != null );
        var tA = (IRecordPocoType)tPoco.Fields[0].Type;
        tA.Fields.Count.Should().Be( 20 );

        // Just to test the code generation.
        using var auto = engineResult.CreateAutomaticServices();
        var p = auto.Services.GetRequiredService<IPocoFactory<IWithLongTuple>>().Create();
        var tuple = (ITuple)p.Long;
        for( int i = 0; i < tuple.Length; i++ )
        {
            tuple[i].Should().BeOfType<string>().And.Be( String.Empty, "The default value for non nullable string is empty." );
        }
    }

    public (List<(int A, (string B, Dictionary<(string C, int /*no name*/, string D), (List<int> E, HashSet<(int F, int G)> H)> /*no name*/) I)>? J, int K) ComplexTupleNames;

    [Test]
    public void complex_tuple_names_handling()
    {
        var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );
        var t = ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ComplexTupleNames ) )! );
        var r0 = CheckIsTuple( t, "J", "K" );
        var r1 = CheckIsTuple( ((ICollectionPocoType)r0.Fields[0].Type).ItemTypes[0], "A", "I" );
        var r2 = CheckIsTuple( r1.Fields[1].Type, "B", "Item2" );
        var dicKey = ((ICollectionPocoType)r2.Fields[1].Type).ItemTypes[0];
        CheckIsTuple( dicKey, "C", "Item2", "D" );
        var dicValue = ((ICollectionPocoType)r2.Fields[1].Type).ItemTypes[1];
        var r3 = CheckIsTuple( dicValue, "E", "H" );
        var setValue = ((ICollectionPocoType)r3.Fields[1].Type).ItemTypes[0];
        CheckIsTuple( setValue, "F", "G" );
    }

    static IRecordPocoType CheckIsTuple( IPocoType? t, params string[] names )
    {
        Throw.DebugAssert( t != null && t is IRecordPocoType );
        var r = (IRecordPocoType)t;
        r.Fields.Select( f => f.Name ).Should().BeEquivalentTo( names );
        return r;
    }
}
