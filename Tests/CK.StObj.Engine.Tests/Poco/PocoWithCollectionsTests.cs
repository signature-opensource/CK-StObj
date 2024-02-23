using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public void readonly_IList_IDictionary_and_ISet_properties_are_automatically_initialized_with_an_empty_instance()
        {

            var c = TestHelper.CreateStObjCollector( typeof( ISimpleCollections ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<ISimpleCollections>>().Create();
            p.Strings.Should().NotBeNull().And.BeEmpty();
            p.Configurations.Should().NotBeNull().And.BeEmpty();
            p.DistinctValues.Should().NotBeNull().And.BeEmpty();
        }

        public interface IWithArray : IPoco
        {
            int[] Array { get; }
        }

        public interface IWithArraySetter : IWithArray
        {
            new int[] Array { get; set; }
        }

        [Test]
        public void non_null_Array_property_are_initialized_to_the_Array_Empty()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithArray ), typeof( IWithArraySetter ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IWithArraySetter>>();
            var p = f.Create();
            p.Array.Should().BeSameAs( Array.Empty<int>() );
            p.Array = new int[] { 1, 2, 3 };
            var readOnly = (IWithArray)p;
            readOnly.Array.Should().BeEquivalentTo( new int[] { 1, 2, 3 } );
        }

        [Test]
        public void read_only_Array_property_are_definitely_empty()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithArray ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IWithArray>>();
            var p = f.Create();
            p.Array.Should().BeSameAs( Array.Empty<int>() );
        }

        public interface IInvalid : IPoco
        {
            IDictionary<string?, bool> Configurations { get; }
        }

        [Test]
        public void IDictionary_key_cannot_be_nullable_even_if_no_constraint_prevent_it()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalid ) );
            TestHelper.GetFailedResult( c, "IDictionary<string,bool>' key cannot be nullable. Nullable type 'string?' cannot be a key." );
        }

        public interface IInvalidAbstractCollectionInside : IPoco
        {
            IList<IList<int>> NoWay { get; }
        }

        [Test]
        public void Abstract_collections_only_at_the_top()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidAbstractCollectionInside ) );
            TestHelper.GetFailedResult( c,
                "Invalid collection 'IList<int>' in Property 'CK.StObj.Engine.Tests.Poco.PocoWithCollectionsTests.IInvalidAbstractCollectionInside.NoWay'." );
        }

        public interface IInvalidConcreteList : IPoco
        {
            List<int> NoWay { get; }
        }

        [Test]
        public void invalid_List_Poco_field()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidConcreteList ) );
            TestHelper.GetFailedResult( c,
                "Invalid concrete collection 'List<int>' in Property 'CK.StObj.Engine.Tests.Poco.PocoWithCollectionsTests.IInvalidConcreteList.NoWay'. Only IList<>, ISet<> and IDictionary<,> must be used for Poco fields." );
        }

    }
}
