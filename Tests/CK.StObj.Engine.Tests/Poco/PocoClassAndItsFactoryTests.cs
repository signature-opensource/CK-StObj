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
using CK.Testing;

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
            var c = TestHelper.CreateTypeCollector( typeof( IPocoKnowsItsFactory ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
            var f = auto.Services.GetRequiredService<IPocoFactory<IPocoKnowsItsFactory>>();
            var o = f.Create();
            var f2 = ((IPocoGeneratedClass)o).Factory;
            f.Should().BeSameAs( f2 );
        }

    }
}
