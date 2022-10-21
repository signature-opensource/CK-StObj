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
    public class PocoValueTupleTests
    {
        public interface IInvalidValueTupleSetter : IPoco
        {
            (int Count, string Name) Thing { get; set; }
        }

        [Test]
        public void anonymous_record_must_be_a_ref_property()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidValueTupleSetter ) );
            TestHelper.GetFailedResult( c );
        }

        public interface IWithValueTuple : IPoco
        {
            (int Count, string Name) Thing { get; set; }
        }

        [Test]
        public void value_tuple_is_Poco_compliant()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithValueTuple ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithValueTuple>>().Create();
            p.Thing = (34, "Test");
            p.Thing.Count.Should().Be( 34 );
            p.Thing.Name.Should().Be( "Test" );
        }

        public interface IValueTupleBase
        {
            (List<int>, Dictionary<string, HashSet<string>>) Thing { get; set; }
        }

        public interface IValueTupleCovariant
        {
            (IReadOnlyList<int>, IReadOnlyDictionary<string, IReadOnlySet<string>>) Thing { get; }

        }

        public class ValueTupleCovariantImplementation : IValueTupleBase, IValueTupleCovariant
        {
            public (List<int>, Dictionary<string, HashSet<string>>) Thing { get; set; }

            (IReadOnlyList<int>, IReadOnlyDictionary<string, IReadOnlySet<string>>) IValueTupleCovariant.Thing
            {
                get
                {
                    return (Thing.Item1, Thing.Item2.AsIReadOnlyDictionary<string, HashSet<string>, IReadOnlySet<string>>());
                }
            }
        }

    }
}
