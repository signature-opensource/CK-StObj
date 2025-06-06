using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

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
    public async Task intrinsic_from_IList_to_IReadOnlyList_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IActualRootA ), typeof( IActualSubA ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var a = d.Create<IActualRootA>();
        a.Lines.ShouldBeAssignableTo<IList<IActualSubA>>();
        a.Lines.ShouldBeAssignableTo<IReadOnlyList<IActualSubA>>();
        a.Lines.ShouldBeAssignableTo<IReadOnlyList<ISubDefiner>>();
        a.Lines.ShouldBeAssignableTo<IReadOnlyList<object>>();
    }

    public interface IActualRootAConcrete : IRootDefiner
    {
        new List<IActualSubA> Lines { get; set; }
    }

    [Test]
    public async Task intrinsic_from_concrete_List_to_IReadOnlyList_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IActualRootAConcrete ), typeof( IActualSubA ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var a = d.Create<IActualRootAConcrete>();
        a.Lines.ShouldBeAssignableTo<IList<IActualSubA>>();
        a.Lines.ShouldBeAssignableTo<IReadOnlyList<IActualSubA>>();
        a.Lines.ShouldBeAssignableTo<IReadOnlyList<ISubDefiner>>();
        a.Lines.ShouldBeAssignableTo<IReadOnlyList<object>>();
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
        TestHelper.GetFailedCollectorResult( [typeof( IInvalidActualRootConcrete ), typeof( IActualSubA )],
            $"Type conflict between:{Environment.NewLine}" +
            $"IList<PocoCovariantPropertyTests.ISubDefiner> CK.StObj.Engine.Tests.Poco.PocoCovariantPropertyTests.IMutableRootDefiner.Lines{Environment.NewLine}" +
            $"And:{Environment.NewLine}" +
            $"List<PocoCovariantPropertyTests.IActualSubA> CK.StObj.Engine.Tests.Poco.PocoCovariantPropertyTests.IInvalidActualRootConcrete.Lines" );
    }

}
