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
        public interface IWithValueTuple : IPoco
        {
            (int Count, string Name) Thing { get; set; }
        }

        [Test]
        public void value_tuple_is_Poco_compliant()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWithValueTuple ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var p = s.GetRequiredService<IPocoFactory<IWithValueTuple>>().Create();
            p.Thing = (34, "Test");
            p.Thing.Count.Should().Be( 34 );
            p.Thing.Name.Should().Be( "Test" );
        }


    }
}
