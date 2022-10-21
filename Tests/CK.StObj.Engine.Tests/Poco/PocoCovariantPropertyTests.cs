using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoCovariantPropertyTests
    {
        [CKTypeDefiner]
        public interface IRootDefiner : IPoco
        {
            IReadOnlyList<ISubDefiner> Lines { get; }
        }

        [CKTypeDefiner]
        public interface ISubDefiner : IPoco
        {
        }

        public interface IActualRootA : IRootDefiner
        {
            new List<IActualSubA> Lines { get; }
        }

        public interface IActualSubA : ISubDefiner
        {
        }

        [Test]
        public void intrinsic_from_List_to_IReadOnlyList()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IActualRootA ), typeof( IActualSubA ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var fA = d.Find( "CK.StObj.Engine.Tests.Poco.PocoCovariantPropertyTests.IActualRootA" );
            Debug.Assert( fA != null );
            var a = (IActualRootA)fA.Create();
            a.Lines.Should().BeOfType<List<IActualSubA>>();
        }

    }
}
