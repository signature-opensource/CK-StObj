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
}
