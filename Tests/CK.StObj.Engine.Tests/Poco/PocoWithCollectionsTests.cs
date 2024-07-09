using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static CK.Testing.MonitorTestHelper;

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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add(typeof( ISimpleCollections ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var p = auto.Services.GetRequiredService<IPocoFactory<ISimpleCollections>>().Create();
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IWithArray ), typeof( IWithArraySetter ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithArraySetter>>();
            var p = f.Create();
            p.Array.Should().BeSameAs( Array.Empty<int>() );
            p.Array = [1, 2, 3];
            var readOnly = (IWithArray)p;
            readOnly.Array.Should().BeEquivalentTo( new int[] { 1, 2, 3 } );
        }

        [Test]
        public void read_only_Array_property_are_definitely_empty()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IWithArray ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithArray>>();
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
            TestHelper.GetFailedCollectorResult( [typeof( IInvalid )], "IDictionary<string,bool>' key cannot be nullable. Nullable type 'string?' cannot be a key." );
        }

        public interface IInvalidAbstractCollectionInside : IPoco
        {
            IList<IList<int>> NoWay { get; }
        }

        [Test]
        public void Abstract_collections_only_at_the_top()
        {
            TestHelper.GetFailedCollectorResult( [typeof( IInvalidAbstractCollectionInside )],
                "Invalid collection 'IList<int>' in Property 'CK.StObj.Engine.Tests.Poco.PocoWithCollectionsTests.IInvalidAbstractCollectionInside.NoWay'." );
        }

        public interface IInvalidConcreteList : IPoco
        {
            List<int> NoWay { get; }
        }

        public interface IValidConcreteList : IPoco
        {
            List<int>? Concrete { get; set; }
        }

        [Test]
        public void List_Poco_field_with_auto_instantiation_is_invalid()
        {
            TestHelper.GetFailedCollectorResult( [typeof( IInvalidConcreteList )],
                "Property 'CK.StObj.Engine.Tests.Poco.PocoWithCollectionsTests.IInvalidConcreteList.NoWay' is a concrete List read only property. " +
                "It must either have a setter { get; set; } or be abstract: 'IList<int> NoWay { get; }'." );
        }

        [Test]
        public void List_Poco_field_with_setter_is_valid()
        {
            TestHelper.GetSuccessfulCollectorResult( [typeof( IValidConcreteList )] );
        }

    }
}
