using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
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
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidValueTupleSetter ) );
            TestHelper.GetFailedResult( c, "' must be a ref property: 'ref (int Count,string Name) Thing { get; }'." );
        }

        public interface IWithValueTuple : IPoco
        {
            ref (int Count, string NotNullableStringDefaultsToEmpty, DateTime DateTimeDefaultsToUtcMinValue) Thing { get; }
        }

        [Test]
        public void non_nullable_string_defaults_to_empty_and_DateTime_defaults_to_Util_UtcMinValue()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithValueTuple ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithValueTuple>>().Create();

            p.Thing.Count.Should().Be( 0 );
            p.Thing.NotNullableStringDefaultsToEmpty.Should().BeEmpty();
            p.Thing.DateTimeDefaultsToUtcMinValue.Should().Be( Util.UtcMinValue );

            p.Thing.Count = 34;
            p.Thing.DateTimeDefaultsToUtcMinValue = Util.UtcMaxValue;

            p.Thing.Count.Should().Be( 34 );
            p.Thing.NotNullableStringDefaultsToEmpty.Should().BeEmpty();
            p.Thing.DateTimeDefaultsToUtcMinValue.Should().Be( Util.UtcMaxValue );
        }

        public interface IWithN : IPoco
        {
            ref (string? A, IList<string?>? B, IList<IList<string?>?>? C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart0 : IPoco
        {
            ref (string? A, IList<string?>? B, IList<IList<string?>?>? C) Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart1 : IPoco
        {
            ref (string? A, IList<string?>? B, IList<IList<string?>?> C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart2 : IPoco
        {
            ref (string? A, IList<string?>? B, IList<IList<string?>>? C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart3 : IPoco
        {
            ref (string? A, IList<string?>? B, IList<IList<string>?>? C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart4 : IPoco
        {
            ref (string? A, IList<string?> B, IList<IList<string?>?>? C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart5 : IPoco
        {
            ref (string? A, IList<string>? B, IList<IList<string?>?>? C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart6 : IPoco
        {
            ref (string A, IList<string?>? B, IList<IList<string?>?>? C)? Thing { get; }
        }

        // Thing is writable: it must have the same nullability.
        public interface INullabilityError0 : IWithN, IWithNotNPart0 { }
        public interface INullabilityError1 : IWithN, IWithNotNPart1 { }
        public interface INullabilityError2 : IWithN, IWithNotNPart2 { }
        public interface INullabilityError3 : IWithN, IWithNotNPart3 { }
        public interface INullabilityError4 : IWithN, IWithNotNPart4 { }
        public interface INullabilityError5 : IWithN, IWithNotNPart5 { }
        public interface INullabilityError6 : IWithN, IWithNotNPart6 { }

        [CKTypeDefiner]
        public interface IWithNPart : IPoco
        {
            ref (string? A, IList<string?>? B, IList<IList<string?>?>? C)? Thing { get; }
        }

        public interface INoError : IWithN, IWithNPart
        {
        }

        [Test]
        public void nullability_incoherence_is_checked()
        {
            var c = TestHelper.CreateStObjCollector( typeof( INoError ) );
            TestHelper.GetSuccessfulResult( c );

            CheckError( typeof( INullabilityError0 ) );
            CheckError( typeof( INullabilityError1 ) );
            CheckError( typeof( INullabilityError2 ) );
            CheckError( typeof( INullabilityError3 ) );
            CheckError( typeof( INullabilityError4 ) );
            CheckError( typeof( INullabilityError5 ) );
            CheckError( typeof( INullabilityError6 ) );

            static void CheckError( Type tError )
            {
                var c = TestHelper.CreateStObjCollector( tError );
                TestHelper.GetFailedResult( c, "Type must be exactly '(string? A,IList<string?>? B,IList<IList<string?>?>? C)?' since " );
            }
        }

        public interface IWithLongTuple : IPoco
        {
            ref ( string F1,
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
                ) Long { get; }
        }


        [Test]
        public void long_value_tuples_are_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithLongTuple ) );
            var result = TestHelper.CreateAutomaticServices( c );
            var ts = result.CollectorResult.CKTypeResult.PocoTypeSystem;

            var tPoco = ts.FindObliviousType<IPrimaryPocoType>( typeof( IWithLongTuple ) );
            Debug.Assert( tPoco != null );
            var tA = (IRecordPocoType)tPoco.Fields[0].Type;
            tA.Fields.Count.Should().Be( 20 );

            // Just to test the code generation.
            using var s = result.Services;
            var p = s.GetRequiredService<IPocoFactory<IWithLongTuple>>().Create();
            var tuple = (ITuple)p.Long;
            for( int i = 0; i < tuple.Length; i++ )
            {
                tuple[i].Should().BeOfType<string>().And.Be( String.Empty, "The default value for non nullable string is empty." );
            }
        }

    }
}
