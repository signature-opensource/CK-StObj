using CK.CodeGen;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using static CK.Testing.StObjEngineTestHelper;
using FluentAssertions;
using System.Reflection;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoClassAndItsFactoryTests
    {

        public interface IPocoKnowsItsFactory : IPoco
        {
            int One { get; set; }
        }

        [Test]
        public void poco_knows_its_Factory()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IPocoKnowsItsFactory ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IPocoKnowsItsFactory>>();
            var o = f.Create();
            var f2 = ((IPocoGeneratedClass)o).Factory;
            f.Should().BeSameAs( f2 );
        }

    }
}
