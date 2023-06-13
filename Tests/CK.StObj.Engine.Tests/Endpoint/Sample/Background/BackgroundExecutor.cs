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
        readonly IEndpointType<BackgroundEndpointDefinition.Data> _endpoint;
        Task _runTask;

        public BackgroundExecutor( IEndpointType<BackgroundEndpointDefinition.Data> endpoint )
        {
            _commands = Channel.CreateUnbounded<object?>();
            _runTask = Task.CompletedTask;
            _endpoint = endpoint;
        }

        public void Push( IActivityMonitor monitor, EndpointUbiquitousInfo info, object command )
        {
            var correlationId = monitor.CreateToken();
            _commands.Writer.TryWrite( new RunCommand( correlationId, info, command ) );
        }

        /// <summary>
        /// Absolutely no protection here. This is JUST for tests!
        /// </summary>
        public void Start() => _runTask = Task.Run( RunAsync );

        public void Stop() => _commands.Writer.TryWrite( null );

        public Task WaitForTerminationAsync() => _runTask;

        sealed record class RunCommand( ActivityMonitor.Token CorrelationId, EndpointUbiquitousInfo UbiquitousInfo, object Command );

        async Task RunAsync()
        {
            var monitor = new ActivityMonitor( "Runner monitor." );
            object? o;
            while( (o = await _commands.Reader.ReadAsync()) != null )
            {
                var cmd = (RunCommand)o;
                // We want any command executed by this loop to use the same monitor.
                using( monitor.StartDependentActivity( cmd.CorrelationId, alwaysOpenGroup: true ) )
                {
                    var data = new BackgroundEndpointDefinition.Data( cmd.UbiquitousInfo, monitor );
                    using( var scope = _endpoint.GetContainer().CreateAsyncScope( data ) )
                    {
                        var executor = scope.ServiceProvider.GetRequiredService<SampleCommandProcessor>();
                        executor.Process( cmd.Command );
                    }
                }
            }
            monitor.MonitorEnd();
        }
    }
}
