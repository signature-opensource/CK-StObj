using CK.Core;
using CK.Engine.TypeCollector;
using CK.Monitoring;
using NUnit.Framework;
using Shouldly;
using System.Linq;
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
        e.IsCompleted.ShouldBeFalse();
        e.CanRun.ShouldBeTrue();
        e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
        h.NakedDone.ShouldBeTrue();
        h.WithParamDone.ShouldBeFalse();

        e.CanRun.ShouldBeFalse();
        e.HasError.ShouldBeFalse();
        e.IsCompleted.ShouldBeFalse( "DoSomethingWith has not been called." );

        var state = e.GetState();
        state.UnsuccessfulCompletedReason.ShouldBe( ReaDIEngineState.UncompletedReason.HasWaitingMethods );
        state.WaitingMethods.ShouldHaveSingleItem().ShouldMatch( m => m.IsWaiting && m.Method.Name == "DoSomethingWith" );
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
            e.IsCompleted.ShouldBeFalse();

            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            h.NakedDone.ShouldBeTrue();

            e.CanRun.ShouldBeFalse();
            e.IsCompleted.ShouldBeFalse();

            e.AddObject( TestHelper.Monitor, this );
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            h.WithParamDone.ShouldBeTrue();

            e.CanRun.ShouldBeFalse();
            e.IsSuccessfullyCompleted.ShouldBeTrue();
            e.GetState().UnsuccessfulCompletedReason.ShouldBe( ReaDIEngineState.UncompletedReason.None );
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

    class MonitorEngineHandler : IReaDIHandler
    {
        [ReaDI]
        public void WithMonitor( IActivityMonitor monitor ) => monitor.Trace( nameof( WithMonitor ) );

        [ReaDI]
        public void WithEngine( ReaDIEngine engine )
        {
            engine.ShouldNotBeNull();
            ActivityMonitor.StaticLogger.Trace( nameof( WithEngine ) );
        }

        [ReaDI]
        public void WithMonitorAndEngine( IActivityMonitor monitor, ReaDIEngine engine )
        {
            engine.ShouldNotBeNull();
            monitor.Trace( nameof( WithMonitorAndEngine ) );
        }

        [ReaDI]
        public void WithMonitor( LoopFreeTests test, IActivityMonitor monitor )
        {
            test.ShouldNotBeNull();
            monitor.Trace( nameof( WithMonitor ) + " + LoopFreeTests" );
        }

        [ReaDI]
        public void WithEngine( ReaDIEngine engine, LoopFreeTests test )
        {
            engine.ShouldNotBeNull();
            ActivityMonitor.StaticLogger.Trace( nameof( WithEngine ) + " + LoopFreeTests" );
        }

        [ReaDI]
        public void WithMonitorAndEngine( LoopFreeTests test, IActivityMonitor monitor, ReaDIEngine engine )
        {
            engine.ShouldNotBeNull();
            monitor.Trace( nameof( WithMonitorAndEngine ) + " + LoopFreeTests" );
        }

    }

    [Test]
    public void with_monitor_and_ReaDIEngine()
    {
        using( var logs = GrandOutput.Default!.CreateMemoryCollector( 200 ) )
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new MonitorEngineHandler();

            e.AddObject( TestHelper.Monitor, this ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();

            e.CanRun.ShouldBeTrue();
            e.RunAll( TestHelper.Monitor ).ShouldBeTrue();

            logs.ExtractCurrentTexts().Where( t => t.StartsWith( "With" ) ).ShouldBe( [
                    "WithMonitor",
                    "WithEngine",
                    "WithMonitorAndEngine",
                    "WithMonitor + LoopFreeTests",
                    "WithEngine + LoopFreeTests",
                    "WithMonitorAndEngine + LoopFreeTests"
                ], ignoreOrder: true );

            e.CanRun.ShouldBeFalse();
        }
    }

    [Test]
    public void with_monitor_and_ReaDIEngine_deferred_object()
    {
        using( var logs = GrandOutput.Default!.CreateMemoryCollector( 200 ) )
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new MonitorEngineHandler();

            e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();

            e.CanRun.ShouldBeTrue();
            e.RunAll( TestHelper.Monitor ).ShouldBeTrue();

            logs.ExtractCurrentTexts().Where( t => t.StartsWith( "With" ) ).ShouldBe( [
                    "WithMonitor",
                    "WithEngine",
                    "WithMonitorAndEngine"
                ], ignoreOrder: true );

            e.CanRun.ShouldBeFalse();

            e.AddObject( TestHelper.Monitor, this ).ShouldBeTrue();

            e.CanRun.ShouldBeTrue();
            e.RunAll( TestHelper.Monitor ).ShouldBeTrue();

            logs.ExtractCurrentTexts().Where( t => t.StartsWith( "With" ) ).ShouldBe( [
                    "WithMonitor + LoopFreeTests",
                    "WithEngine + LoopFreeTests",
                    "WithMonitorAndEngine + LoopFreeTests"
                ], ignoreOrder: true );

            e.CanRun.ShouldBeFalse();
        }
    }
}
