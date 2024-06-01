using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Serialization;
using NUnit.Framework;
using System;
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
        public void intrinsic_from_IList_to_IReadOnlyList()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IActualRootA ), typeof( IActualSubA ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var a = d.Create<IActualRootA>();
            a.Lines.Should().BeAssignableTo<IList<IActualSubA>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<IActualSubA>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<ISubDefiner>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<object>>();
        }

        public interface IActualRootAConcrete : IRootDefiner
        {
            new List<IActualSubA> Lines { get; set; }
        }

        [Test]
        public void intrinsic_from_concrete_List_to_IReadOnlyList()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IActualRootAConcrete ), typeof( IActualSubA ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var a = d.Create<IActualRootAConcrete>();
            a.Lines.Should().BeAssignableTo<IList<IActualSubA>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<IActualSubA>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<ISubDefiner>>();
            a.Lines.Should().BeAssignableTo<IReadOnlyList<object>>();
        }

        [CKTypeDefiner]
        public interface IMutableRootDefiner : IPoco
        {
            IList<ISubDefiner> Lines { get; }
        }

        public interface IInvalidActualRootConcrete : IMutableRootDefiner
        {
            new List<IActualSubA> Lines { get; set; }
        }

        [Test]
        public void invalid_abstract_to_concrete()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IInvalidActualRootConcrete ), typeof( IActualSubA ) );
            TestHelper.GetFailedCollectorResult( c,
                $"Type conflict between:{Environment.NewLine}" +
                $"IList<PocoCovariantPropertyTests.ISubDefiner> CK.StObj.Engine.Tests.Poco.PocoCovariantPropertyTests.IMutableRootDefiner.Lines{Environment.NewLine}" +
                $"And:{Environment.NewLine}" +
                $"List<PocoCovariantPropertyTests.IActualSubA> CK.StObj.Engine.Tests.Poco.PocoCovariantPropertyTests.IInvalidActualRootConcrete.Lines" );
        }

    }
}
