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
        readonly IFakeAuthenticationInfo _userInfo;

        public SampleCommandProcessor( IActivityMonitor monitor, SampleCommandMemory commandHistory, IFakeAuthenticationInfo info )
        {
            _monitor = monitor;
            _commandHistory = commandHistory;
            _userInfo = info;
        }

        public void Process( object command )
        {
            _commandHistory.Trace( $"{command} - {_userInfo.UserName} - {_userInfo.ActorId}" );
            _monitor.Info( $"Processed command '{command}' (for user '{_userInfo.UserName}' ({_userInfo.ActorId})." );
        }
    }
}
