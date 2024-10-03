using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.StObj.Engine.Tests.Endpoint;

public class BackgroundExecutorService : ISingletonAutoService
{
    readonly Channel<object?> _commands;
    readonly IDIContainer<BackgroundDIContainerDefinition.Data> _endpoint;
    Task _runTask;

    public BackgroundExecutorService( IDIContainer<BackgroundDIContainerDefinition.Data> endpoint )
    {
        _commands = Channel.CreateUnbounded<object?>();
        _runTask = Task.CompletedTask;
        _endpoint = endpoint;
    }

    public void Push( IActivityMonitor monitor, AmbientServiceHub info, object command )
    {
        var correlationId = monitor.CreateToken();
        _commands.Writer.TryWrite( new RunCommand( correlationId, info, command, null ) );
    }

    public Task RunAsync( IActivityMonitor monitor, AmbientServiceHub info, object command )
    {
        var correlationId = monitor.CreateToken();
        var tcs = new TaskCompletionSource();
        _commands.Writer.TryWrite( new RunCommand( correlationId, info, command, tcs ) );
        return tcs.Task;
    }

    public Action<IServiceProvider>? CheckBackgroundServices { get; set; }

    /// <summary>
    /// Absolutely no protection here. This is JUST for tests!
    /// </summary>
    public void Start() => _runTask = Task.Run( RunAsync );

    public void Stop() => _commands.Writer.TryWrite( null );

    public Task WaitForTerminationAsync() => _runTask;

    sealed record class RunCommand( ActivityMonitor.Token CorrelationId, AmbientServiceHub AmbientServiceHub, object Command, TaskCompletionSource? TCS );

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
                var data = new BackgroundDIContainerDefinition.Data( cmd.AmbientServiceHub, monitor );
                await using( var scope = _endpoint.GetContainer().CreateAsyncScope( data ) )
                {
                    CheckBackgroundServices?.Invoke( scope.ServiceProvider );
                    try
                    {
                        ISampleCommandProcessor executor;
                        if( cmd.Command is ICommandThatMustBeProcessedBy known )
                        {
                            executor = (ISampleCommandProcessor)scope.ServiceProvider.GetRequiredService( known.GetCommandProcessorType() );
                        }
                        else
                        {
                            executor = scope.ServiceProvider.GetRequiredService<DefaultCommandProcessor>();
                        }
                        executor.Process( cmd.Command );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( ex );
                    }
                    cmd.TCS?.SetResult();
                }
            }
        }
        monitor.MonitorEnd();
    }
}
