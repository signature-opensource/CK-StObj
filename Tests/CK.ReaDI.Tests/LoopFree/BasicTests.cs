using CK.Core;
using CK.Engine.TypeCollector;
using CK.Monitoring;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using System.Reflection.Metadata;
using static CK.Testing.MonitorTestHelper;

namespace CK.ReaDI.LoopFree.Tests;

[TestFixture]
public class ContravarianceTests
{

}

[TestFixture]
public class BasicTests
{
    class MostBasicHandler : IReaDIHandler
    {
        public bool NakedDone { get; private set; }
        public bool WithParamDone { get; private set; }

        [ReaDI]
        public void DoSomething() => NakedDone = true;

        [ReaDI]
        public void DoSomethingWith( BasicTests o ) => WithParamDone = true;
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
        public void WithMonitor( BasicTests test, IActivityMonitor monitor )
        {
            test.ShouldNotBeNull();
            monitor.Trace( nameof( WithMonitor ) + " + LoopFreeTests" );
        }

        [ReaDI]
        public void WithEngine( ReaDIEngine engine, BasicTests test )
        {
            engine.ShouldNotBeNull();
            ActivityMonitor.StaticLogger.Trace( nameof( WithEngine ) + " + LoopFreeTests" );
        }

        [ReaDI]
        public void WithMonitorAndEngine( BasicTests test, IActivityMonitor monitor, ReaDIEngine engine )
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


    class BaseHandler : IReaDIHandler
    {
        [ReaDI]
        public void BaseAction( IActivityMonitor monitor )
        {
            monitor.Trace( $"BaseAction from {GetType().Name}" );
        }
    }

    class HandlerA : BaseHandler
    {
        [ReaDI]
        public void MoreAction( IActivityMonitor monitor )
        {
            monitor.Trace( "MoreAction from A" );
        }
    }

    class HandlerB : BaseHandler
    {
        [ReaDI]
        public void MoreAction( IActivityMonitor monitor )
        {
            monitor.Trace( "MoreAction from B" );
        }
    }

    [Test]
    public void handlers_can_be_specialized()
    {
        var e = new ReaDIEngine( new GlobalTypeCache() );
        var common = new BaseHandler();
        var hA = new HandlerA();
        var hB = new HandlerB();

        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            e.AddObject( TestHelper.Monitor, this ).ShouldBeTrue();

            e.AddObject( TestHelper.Monitor, common ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, hA ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, hB ).ShouldBeTrue();

            e.CanRun.ShouldBeTrue();
            e.RunAll( TestHelper.Monitor ).ShouldBeTrue();

            //Ordering from GetMembers is respected.
            logs.ShouldBe( [ "BaseAction from BaseHandler",
                             "MoreAction from A",
                             "BaseAction from HandlerA",
                             "MoreAction from B",
                             "BaseAction from HandlerB" ] );
            e.AllCallables.Select( c => c.ToString() )
                          .ShouldBe( [
                            "Void CK.ReaDI.LoopFree.Tests.BasicTests.BaseHandler.BaseAction( IActivityMonitor monitor )",
                            "Void CK.ReaDI.LoopFree.Tests.BasicTests.HandlerA.MoreAction( IActivityMonitor monitor )",
                            "Void CK.ReaDI.LoopFree.Tests.BasicTests.HandlerA.BaseAction( IActivityMonitor monitor )",
                            "Void CK.ReaDI.LoopFree.Tests.BasicTests.HandlerB.MoreAction( IActivityMonitor monitor )",
                            "Void CK.ReaDI.LoopFree.Tests.BasicTests.HandlerB.BaseAction( IActivityMonitor monitor )"
                            ] );
        }
    }


    class VBaseHandler : IReaDIHandler
    {
        [ReaDI]
        public virtual void BaseAction( IActivityMonitor monitor )
        {
            monitor.Trace( $"BaseAction from {GetType().Name}." );
        }
    }

    class VHandlerA : VBaseHandler
    {
        [ReaDI]
        public override void BaseAction( IActivityMonitor monitor )
        {
            monitor.Trace( $"BaseAction in VHandlerA (regular override of the BaseAction)." );
        }
    }

    class VHandlerB : VBaseHandler
    {
        [ReaDI]
        public new void BaseAction( IActivityMonitor monitor )
        {
            monitor.Trace( $"new BaseAction in VHandlerB (BaseAction is also called)." );
        }
    }

    [Test]
    public void handlers_can_be_specialized_and_virtual_or_not_are_handled()
    {
        var e = new ReaDIEngine( new GlobalTypeCache() );
        var common = new VBaseHandler();
        var hA = new VHandlerA();
        var hB = new VHandlerB();

        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            e.AddObject( TestHelper.Monitor, this ).ShouldBeTrue();

            e.AddObject( TestHelper.Monitor, common ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, hA ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, hB ).ShouldBeTrue();

            e.CanRun.ShouldBeTrue();
            e.RunAll( TestHelper.Monitor ).ShouldBeTrue();

            logs.ShouldBe( [ "BaseAction from VBaseHandler.",
                             "BaseAction in VHandlerA (regular override of the BaseAction).",
                             "new BaseAction in VHandlerB (BaseAction is also called).",
                             "BaseAction from VHandlerB." ] );
        }
    }

    class BadHandler : IReaDIHandler
    {
        [ReaDI]
        public virtual void BaseAction( BasicTests p1, BasicTests p2 )
        {
        }
    }


    [Test]
    public void duplicate_parameters_are_forbidden()
    {
        var e = new ReaDIEngine( new GlobalTypeCache() );
        var bad = new BadHandler();

        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            // Adding the object (or not) is irrelevant: the handler will be rejected.
            e.AddObject( TestHelper.Monitor, this ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, bad ).ShouldBeFalse();

            logs.ShouldContain( "Duplicate parameter types in 'Void BaseAction( BasicTests p1, BasicTests p2 )': 'p2' and 'p1' are both 'CK.ReaDI.LoopFree.Tests.BasicTests'." );
        }
    }


}
