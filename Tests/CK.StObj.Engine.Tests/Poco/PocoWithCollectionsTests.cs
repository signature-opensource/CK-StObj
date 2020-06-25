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

            public List<string> StringList { get; }

            public IDictionary<string, ISimpleCollections> Configurations { get; }

            public Dictionary<string, ISimpleCollections> ConfigurationDictionary { get; }

            public ISet<int> DistinctValues { get; }

            public ISet<int> DistinctValueSet { get; }
        }

        [Test]
        public void List_Dictionary_and_Set_properties_with_getter_only_are_automatically_initialized_with_an_empty_instance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ISimpleCollections ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<ISimpleCollections>>().Create();
            p.Strings.Should().NotBeNull().And.BeEmpty();
            p.StringList.Should().NotBeNull().And.BeEmpty();
            p.Configurations.Should().NotBeNull().And.BeEmpty();
            p.ConfigurationDictionary.Should().NotBeNull().And.BeEmpty();
            p.DistinctValues.Should().NotBeNull().And.BeEmpty();
            p.DistinctValueSet.Should().NotBeNull().And.BeEmpty();

            p.GetType().GetProperty( nameof( p.Strings ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.StringList ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.Configurations ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.ConfigurationDictionary ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.DistinctValues ) ).Should().NotBeWritable();
            p.GetType().GetProperty( nameof( p.DistinctValueSet ) ).Should().NotBeWritable();
        }

        public interface INotCollections : IPoco
        {
            public IEnumerable<int> Enumerable { get; }

            public PocoWithCollectionsTests ComplexClass { get; }

            public float SimpleValueType { get; }
        }

        [Test]
        public void ReadOnly_Properties_that_are_not_IPoco_List_Dictionary_or_Set_are_not_initialized_and_have_an_implemented_Setter()
        {
            var c = TestHelper.CreateStObjCollector( typeof( INotCollections ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<INotCollections>>().Create();
            p.Enumerable.Should().BeNull();
            p.ComplexClass.Should().BeNull();
            p.SimpleValueType.Should().Be( 0.0f );

            p.GetType().GetProperty( nameof( p.Enumerable ) ).Should().BeWritable();
            p.GetType().GetProperty( nameof( p.ComplexClass ) ).Should().BeWritable();
            p.GetType().GetProperty( nameof( p.SimpleValueType ) ).Should().BeWritable();
        }

    }
}
