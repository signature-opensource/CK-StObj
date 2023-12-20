using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
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
            new IList<IActualSubA> Lines { get; }
        }

        public interface IActualSubA : ISubDefiner
        {
        }

        [Test]
        [Ignore( "Not implemented yet." )]
        public void intrinsic_from_IList_to_IReadOnlyList()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IActualRootA ), typeof( IActualSubA ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var fA = d.Find( "CK.StObj.Engine.Tests.Poco.PocoCovariantPropertyTests.IActualRootA" );
            Debug.Assert( fA != null );
            var a = (IActualRootA)fA.Create();
            a.Lines.Should().BeAssignableTo<IList<IActualSubA>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<IActualSubA>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<ISubDefiner>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<object>>();
        }

    }
}