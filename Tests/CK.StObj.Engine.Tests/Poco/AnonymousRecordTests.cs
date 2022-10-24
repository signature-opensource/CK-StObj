using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
            TestHelper.GetFailedResult( c );
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
            ref (string? A, List<string>? B, List<List<string>?> C)? Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNotNPart : IPoco
        {
            ref (string? A, List<string>? B, List<List<string>?> C) Thing { get; }
        }

        [CKTypeDefiner]
        public interface IWithNPart : IPoco
        {
            ref (string? A, List<string>? B, List<List<string>?> C)? Thing { get; }
        }

        public interface INullabilityError : IWithN, IWithNotNPart
        {
            // Thing is writable: it must have the same nullability.
        }

        [Test]
        public void nullability_incoherence_is_checked()
        {
            var c = TestHelper.CreateStObjCollector( typeof( INullabilityError ) );
            TestHelper.GetFailedResult( c );
        }
    }
}
