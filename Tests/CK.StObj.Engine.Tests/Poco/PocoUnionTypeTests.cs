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
    public class PocoUnionTypeTests
    {
        public interface IInvalidPocoWithUnionType : IPoco
        {
            [UnionType( typeof( string ), typeof( int ) )]
            string Thing { get; set; }
        }

        [Test]
        public void Union_property_can_only_be_applied_to_object()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidPocoWithUnionType ) );
            TestHelper.GetFailedResult( c );
        }

        public interface IPocoWithUnionType : IPoco
        {
            [UnionType( typeof( string ), typeof( int ) )]
            object Thing { get; set; }
        }

        [Test]
        public void Union_property_guard_the_allowed_types_and_null_is_always_allowed()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithUnionType ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IPocoWithUnionType>>().Create();

            p.Thing = 34;
            p.Thing.Should().Be( 34 );
            p.Thing = "lklk";
            p.Thing.Should().Be( "lklk" );
            p.Thing = null!;
            p.Thing.Should().BeNull();

            p.Invoking( x => x.Thing = 25.88 ).Should().Throw<ArgumentException>();
            p.Invoking( x => x.Thing = this ).Should().Throw<ArgumentException>();
        }


    }
}
