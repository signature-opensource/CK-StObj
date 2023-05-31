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
        readonly IEndpointServiceProvider<BackgroundEndpointDefinition.BackgroundData> _serviceProvider;
        readonly Channel<object?> _commands;
        readonly Task _runTask;

        public BackgroundExecutor( IEndpointType<BackgroundEndpointDefinition.BackgroundData> endpoint )
        {
            _serviceProvider = endpoint.GetContainer();
            _commands = Channel.CreateUnbounded<object?>();
            _runTask = Task.Run( RunAsync );
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
            // We want any command executed by this loop to use the same monitor.
            var data = new BackgroundEndpointDefinition.BackgroundData( monitor );
            object? o;
            while( (o = await _commands.Reader.ReadAsync()) != null )
            {
                var cmd = (RunCommand)o;
                data.Auth = cmd.Auth;
                using( var scope = _serviceProvider.CreateAsyncScope( data ) )
                {
                    var executor = scope.ServiceProvider.GetRequiredService<SampleCommandProcessor>();
                    executor.Process( cmd.Command );
                }
            }
            monitor.MonitorEnd();
        }
    }
}
