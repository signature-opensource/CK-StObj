using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public partial class PocoUnionTypeTests
    {

        public interface IPocoNonExtendable : IPoco
        {
            [UnionType]
            object Thing { get; set; }

            class UnionTypes
            {
                public (decimal, int, double) Thing { get; }
            }
        }

        public interface IPocoNonExtendableSpecializedMore : IPocoNonExtendable
        {
            [UnionType]
            new object Thing { get; set; }

            new class UnionTypes
            {
                public (decimal, int, double, string) Thing { get; }
            }
        }

        public interface IPocoNonExtendableSpecializedLess : IPocoNonExtendable
        {
            [UnionType]
            new object Thing { get; set; }

            new class UnionTypes
            {
                public (decimal, int) Thing { get; }
            }
        }

        [Test]
        public void Union_property_types_cannot_be_extended_by_default()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoNonExtendable ), typeof( IPocoNonExtendableSpecializedMore ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IPocoNonExtendable ), typeof( IPocoNonExtendableSpecializedLess ) );
            TestHelper.GetFailedResult( c );
        }

        public interface IPocoNonExtendableIndependent : IPoco
        {
        }

        public interface IPocoNonExtendableIndependentProperty : IPocoNonExtendableIndependent
        {
            [UnionType]
            object? AnotherThing { get; set; }

            class UnionTypes
            {
                public (string[]?, string?, List<string>?) AnotherThing { get; }
            }
        }

        public interface IPocoNonExtendableIndependentLess : IPocoNonExtendableIndependent
        {
            [UnionType]
            object? AnotherThing { get; set; }

            class UnionTypes
            {
                public (string[]?, string?) AnotherThing { get; }
            }
        }

        public interface IPocoNonExtendableIndependentMore : IPocoNonExtendableIndependent
        {
            [UnionType]
            object? AnotherThing { get; set; }

            class UnionTypes
            {
                public (string[]?, string?, IList<string>?, ISet<string>?) AnotherThing { get; }
            }
        }

        [Test]
        public void Union_property_types_cannot_be_extended_by_default_accross_independent_interfaces()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoNonExtendableIndependent ), typeof( IPocoNonExtendableIndependentProperty ), typeof( IPocoNonExtendableIndependentLess ) );
            TestHelper.GetFailedResult( c );
            c = TestHelper.CreateStObjCollector( typeof( IPocoNonExtendableIndependent ), typeof( IPocoNonExtendableIndependentProperty ), typeof( IPocoNonExtendableIndependentMore ) );
            TestHelper.GetFailedResult( c );
        }

        public interface IPoco1 : IPoco
        {
            [UnionType( CanBeExtended = true )]
            object Thing { get; set; }

            class UnionTypes
            {
                public (decimal, int) Thing { get; }
            }
        }

        public interface IPoco2 : IPoco1
        {
            [UnionType( CanBeExtended = true )]
            new object Thing { get; set; }

            [UnionType( CanBeExtended = true )]
            object? AnotherThing { get; set; }

            new class UnionTypes
            {
                public (string, IList<string>) Thing { get; }

                public (int, double)? AnotherThing { get; }
            }
        }

        public interface IPoco2Bis : IPoco1
        {
            [UnionType( CanBeExtended = true )]
            object? AnotherThing { get; set; }

            new class UnionTypes
            {
                public (string,IList<string?>)? AnotherThing { get; }
            }
        }

        [Test]
        public void Union_types_can_be_extendable_as_long_as_CanBeExtended_is_specified()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPoco1 ), typeof( IPoco2 ), typeof( IPoco2Bis ), typeof( PocoJsonSerializer ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var p = s.GetRequiredService<IPocoFactory<IPoco2>>().Create();

            // Thing allows int, decimal, string and List<string> (not nullable!)
            p.Thing = 34;
            p.Thing.Should().Be( 34 );
            p.Thing = (decimal)555;
            p.Thing.Should().Be( (decimal)555 );
            p.Thing = "It works!";
            p.Thing.Should().Be( "It works!" );

            p.Invoking( x => x.Thing = null! ).Should().Throw<ArgumentException>( "Null is forbidden." );
            p.Invoking( x => x.Thing = new Dictionary<string,object>() ).Should().Throw<ArgumentException>( "Not an allowed type." );

            var p2 = JsonTestHelper.Roundtrip( directory, p, text: t => TestHelper.Monitor.Info( t ) );
            p.Should().BeEquivalentTo( p2 );

            // AnotherThing allows int, double?, string? and List<string?>?
            p.AnotherThing = 34;
            p.AnotherThing.Should().Be( 34 );
            p.AnotherThing = 0.04e-5;
            p.AnotherThing = null;
            p.AnotherThing.Should().BeNull();

            p.Invoking( x => x.AnotherThing = (Decimal)555 ).Should().Throw<ArgumentException>( "Not an allowed type." );
            p.Invoking( x => x.AnotherThing = new Dictionary<string, object>() ).Should().Throw<ArgumentException>( "Not an allowed type." );

            var p3 = JsonTestHelper.Roundtrip( directory, p );
            p.Should().BeEquivalentTo( p3 );
        }


    }
}
