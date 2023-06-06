using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public class BackgroundExecutor : ISingletonAutoService
    {
        readonly Channel<object?> _commands;
        readonly Task _runTask;
        readonly IEndpointType<BackgroundEndpointDefinition.BackgroundData> _endpoint;

        public BackgroundExecutor( IEndpointType<BackgroundEndpointDefinition.BackgroundData> endpoint )
        {
            _commands = Channel.CreateUnbounded<object?>();
            _runTask = Task.Run( RunAsync );
            _endpoint = endpoint;
        }

        public void Push( IFakeAuthenticationInfo? info, object command )
        {
            _commands.Writer.TryWrite( new RunCommand( info, command ) );
        }

        public void Stop() => _commands.Writer.TryWrite( null );

        public Task WaitForTerminationAsync() => _runTask;

        sealed record class RunCommand( IFakeAuthenticationInfo? Auth, object Command );

        async Task RunAsync()
        {
            var monitor = new ActivityMonitor( "Background Executor." );
            object? o;
            while( (o = await _commands.Reader.ReadAsync()) != null )
            {
                var cmd = (RunCommand)o;
                // We want any command executed by this loop to use the same monitor.
                var data = new BackgroundEndpointDefinition.BackgroundData( _endpoint, monitor, cmd.Auth ?? IFakeAuthenticationInfo.Anonymous );
                using( var scope = _endpoint.GetContainer().CreateAsyncScope( data ) )
                {
                    var executor = scope.ServiceProvider.GetRequiredService<SampleCommandProcessor>();
                    executor.Process( cmd.Command );
                }
            }
            monitor.MonitorEnd();
        }
    }
}
