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
    public class PocoWithCollectionsTests
    {
        public interface ISimpleCollections : IPoco
        {
            public IList<string> Strings { get; }

            public IDictionary<string, ISimpleCollections> Configurations { get; }

            public ISet<int> DistinctValues { get; }
        }

        [Test]
        public void IList_IDictionary_and_ISet_properties_with_getter_only_are_automatically_initialized_with_an_empty_instance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ISimpleCollections ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<ISimpleCollections>>().Create();
            p.Strings.Should().NotBeNull().And.BeEmpty();
            p.Configurations.Should().NotBeNull().And.BeEmpty();
            p.DistinctValues.Should().NotBeNull().And.BeEmpty();

            p.GetType().GetProperty( nameof( p.Strings ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.Configurations ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.DistinctValues ) ).Should().NotBeWritable();
        }

        public interface IWithListString : IPoco
        {
            IList<string> L { get; }
        }

        public interface IWithListNullableString : IWithListString
        {
            new IList<string?> L { get; }
        }

        public interface IWithNullableListString : IWithListString
        {
            new IList<string>? L { get; }
        }

        [Test]
        public void NRT_must_be_the_same_across_Poco_family()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithListString ), typeof( IWithListNullableString ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IWithListString ), typeof( IWithNullableListString ) );
            TestHelper.GetFailedResult( c );
        }


        public interface IArrayMustHaveSetter : IPoco
        {
            int[] A { get; }
        }

        [Test]
        public void Array_property_cannot_be_auto_instantiated()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IArrayMustHaveSetter ) );
            TestHelper.GetFailedResult( c );
        }


    }
}
