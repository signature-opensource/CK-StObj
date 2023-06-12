using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{


    /// <summary>
    /// Simulates a simple command handler that requires a monitor and
    /// an authentication info.
    /// </summary>
    public sealed class SampleCommandProcessor : IScopedAutoService
    {
        readonly IActivityMonitor _monitor;
        readonly SampleCommandMemory _commandHistory;
        readonly IFakeTenantInfo _tenantInfo;

        public SampleCommandProcessor( IActivityMonitor monitor, SampleCommandMemory commandHistory, IFakeTenantInfo info )
        {
            _monitor = monitor;
            _commandHistory = commandHistory;
            _tenantInfo = info;
        }

        public void Process( object command )
        {
            _commandHistory.Trace( $"{command} - {_tenantInfo.Name}" );
            _monitor.Info( $"Processed command '{command}' (in tenant '{_tenantInfo.Name}'." );
        }
    }
}
