using CK.Core;
using FluentAssertions;

namespace CK.StObj.Engine.Tests.SimpleObjects.LoggerInjection
{
    public class LoggerInjected : IRealObject
    {
        void StObjConstruct( IActivityMonitor monitor, IActivityMonitor anotherLogger = null )
        {
            monitor.Should().NotBeNull( "This is the Setup monitor." );
            anotherLogger.Should().BeSameAs( monitor, "All IActivityMonitor are Setup monitors." );
            monitor.Trace( "Setup monitor can be used by StObjConstruct method.");
        }
    }
}
