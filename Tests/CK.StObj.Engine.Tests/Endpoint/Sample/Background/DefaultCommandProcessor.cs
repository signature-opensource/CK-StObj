using CK.Core;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// Simulates a simple command handler that can handle any type of command but
    /// requires a monitor and a tenant info.
    /// </summary>
    public sealed class DefaultCommandProcessor : ISampleCommandProcessor, IScopedAutoService
    {
        readonly IActivityMonitor _monitor;
        readonly SampleCommandMemory _commandHistory;
        readonly IFakeTenantInfo _tenantInfo;

        public DefaultCommandProcessor( IActivityMonitor monitor, SampleCommandMemory commandHistory, IFakeTenantInfo info )
        {
            _monitor = monitor;
            _commandHistory = commandHistory;
            _tenantInfo = info;
        }

        public void Process( object command )
        {
            _commandHistory.Trace( $"{command} - {_tenantInfo.Name} - {_monitor.Topic}" );
            _monitor.Info( $"Processed command '{command}' (in tenant '{_tenantInfo.Name}'." );
        }
    }
}
