using CK.Core;
using CK.Demo;
using CK.Engine.TypeCollector;
using NUnit.Framework;
using Shouldly;
using static CK.Testing.MonitorTestHelper;

namespace CK.ReaDI.Tests;

[TestFixture]
public class BasicLoopTests
{
    sealed class ConfigurationConsumer : IReaDIHandler
    {
        [ReaDI]
        public void DumpConfiguration( IActivityMonitor monitor, EngineConfiguration configuration )
        {
            monitor.Trace( $"EngineConfiguration: {configuration}" );
        }
    }

    [Test]
    public void loop_root_only()
    {
        var config = new EngineConfiguration() { DebugMode = true };
        {
            var e = new ReaDIEngine( new GlobalTypeCache(), config.DebugMode );
            var h = new ConfigurationConsumer();
            // Handler first.
            e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();
            e.AddObject( TestHelper.Monitor, new EngineConfiguration() );
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            e.CanRun.ShouldBeFalse();
        }
        {
            var e = new ReaDIEngine( new GlobalTypeCache(), config.DebugMode );
            var h = new ConfigurationConsumer();
            // Config first.
            e.AddObject( TestHelper.Monitor, new EngineConfiguration() );
            e.AddObject( TestHelper.Monitor, h ).ShouldBeTrue();
            e.CanRun.ShouldBeTrue();
            e.RunOne( TestHelper.Monitor ).ShouldBeTrue();
            e.CanRun.ShouldBeFalse();
        }
    }

    [HierarchicalTypeRoot]
    class Loop1 { }

    [HierarchicalType<Loop1>]
    class SubLoop1 { }

    [HierarchicalTypeRoot]
    class Loop2 { }

    [HierarchicalType<Loop2>]
    class SubLoop2 { }

    class UnsupportedCrossLoopHandler1 : IReaDIHandler
    {
        [ReaDI]
        public void Do( [ReaDILoop] Loop1 p1, [ReaDILoop] Loop2 p2 ) { }
    }

    class UnsupportedCrossLoopHandler2 : IReaDIHandler
    {
        [ReaDI]
        public void Do( [ReaDILoop] Loop1 p1, [ReaDILoop] SubLoop2 p2 ) { }
    }

    class UnsupportedCrossLoopHandler3 : IReaDIHandler
    {
        [ReaDI]
        public void Do( [ReaDILoop] SubLoop1 p1, [ReaDILoop] SubLoop2 p2 ) { }
    }


    [Test]
    public void cross_product_loop_is_not_supported()
    {
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new UnsupportedCrossLoopHandler1();
            e.AddObject( TestHelper.Monitor, h ).ShouldBeFalse();
            logs.ShouldContain( """
                Invalid loop parameters:
                'p1' in 'Void Do( [ReaDILoop]Loop1 p1, [ReaDILoop]Loop2 p2 )' is subordinated to root type 'CK.ReaDI.Tests.BasicLoopTests.Loop1',
                and 'p2' is subordinated to root type 'CK.ReaDI.Tests.BasicLoopTests.Loop2'.
                Cross-product looping is not supported.
                """ );
        }
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new UnsupportedCrossLoopHandler2();
            e.AddObject( TestHelper.Monitor, h ).ShouldBeFalse();
            logs.ShouldContain( """
                Invalid loop parameters:
                'p1' in 'Void Do( [ReaDILoop]Loop1 p1, [ReaDILoop]SubLoop2 p2 )' is subordinated to root type 'CK.ReaDI.Tests.BasicLoopTests.Loop1',
                and 'p2' is subordinated to root type 'CK.ReaDI.Tests.BasicLoopTests.Loop2'.
                Cross-product looping is not supported.
                """ );
        }
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            var e = new ReaDIEngine( new GlobalTypeCache() );
            var h = new UnsupportedCrossLoopHandler3();
            e.AddObject( TestHelper.Monitor, h ).ShouldBeFalse();
            logs.ShouldContain( """
                Invalid loop parameters:
                'p1' in 'Void Do( [ReaDILoop]SubLoop1 p1, [ReaDILoop]SubLoop2 p2 )' is subordinated to root type 'CK.ReaDI.Tests.BasicLoopTests.Loop1',
                and 'p2' is subordinated to root type 'CK.ReaDI.Tests.BasicLoopTests.Loop2'.
                Cross-product looping is not supported.
                """ );
        }
    }
}
