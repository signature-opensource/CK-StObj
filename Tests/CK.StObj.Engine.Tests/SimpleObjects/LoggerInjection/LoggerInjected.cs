using CK.Core;
using Shouldly;

namespace CK.StObj.Engine.Tests.SimpleObjects.LoggerInjection;

public class LoggerInjected : IRealObject
{
    void StObjConstruct( IActivityMonitor monitor, IActivityMonitor? anotherLogger = null )
    {
        monitor.ShouldNotBeNull( "This is the Setup monitor." );
        anotherLogger.ShouldBeSameAs( monitor, "All IActivityMonitor are Setup monitors." );
        monitor.Trace( "Setup monitor can be used by StObjConstruct method." );
    }
}
