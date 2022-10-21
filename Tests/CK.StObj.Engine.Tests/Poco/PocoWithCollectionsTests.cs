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
            public List<string> Strings { get; }

            public Dictionary<string, ISimpleCollections> Configurations { get; }

            public HashSet<int> DistinctValues { get; }
        }

        [Test]
        public void readonly_List_Dictionary_and_HashSet_properties_are_automatically_initialized_with_an_empty_instance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ISimpleCollections ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<ISimpleCollections>>().Create();
            p.Strings.Should().NotBeNull().And.BeEmpty();
            p.Configurations.Should().NotBeNull().And.BeEmpty();
            p.DistinctValues.Should().NotBeNull().And.BeEmpty();

            p.GetType().GetProperty( nameof( p.Strings ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.Configurations ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.DistinctValues ) ).Should().NotBeWritable();
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
            var c = TestHelper.CreateStObjCollector( typeof( IWithArray ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IWithArray>>();
            var p = f.Create();
            p.Array.Should().BeSameAs( Array.Empty<int>() );
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

    }
}
