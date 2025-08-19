using CK.Core;
using CK.Engine.TypeCollector;
using NUnit.Framework;
using Shouldly;
using static CK.Testing.MonitorTestHelper;

namespace CK.ReaDI.Tests;

[TestFixture]
public class LoopFreeTests
{
    class MostBasicHandler : IReaDIHandler
    {
        public bool NakedDone { get; private set; }
        public bool WithParamDone { get; private set; }

        [ReaDI]
        public void DoSomething() => NakedDone = true;

        [ReaDI]
        public void DoSomethingWith( LoopFreeTests o ) => WithParamDone = true;
    }

    [Test]
    public void minimal_run_has_no_parameter()
    {
        var e = new ReaDIEngine( new GlobalTypeCache() );
        var h = new MostBasicHandler();
        e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();
        e.CanRun.ShouldBeTrue();
        e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
        h.NakedDone.ShouldBeTrue();
        e.CanRun.ShouldBeFalse();
    }

    [Test]
    public void minimal_run_with_one_object()
    {
        // Adding the required parameter object after the handler.
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new MostBasicHandler();
            e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            h.NakedDone.ShouldBeTrue();
            e.CanRun.ShouldBeFalse();
            e.AddObject( TestHelper.Monitor, this );
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            h.WithParamDone.ShouldBeTrue();
            e.CanRun.ShouldBeFalse();
        }
        // Adding the required parameter object before the handler.
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new MostBasicHandler();
            // We can know here that the naked method is the first callable
            // only because of the ordering of the DeclaredMembers...
            e.AddObject( TestHelper.Monitor, this );
            e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            h.NakedDone.ShouldBeTrue();
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            h.WithParamDone.ShouldBeTrue();
            e.CanRun.ShouldBeFalse();
        }
    }


}
