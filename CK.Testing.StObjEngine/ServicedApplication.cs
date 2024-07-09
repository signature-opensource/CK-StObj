using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Testing
{
    /// <summary>
    /// Helper that fixes the <see cref="IHost"/> abstraction by hiding the <see cref="IDisposable"/> and
    /// exposes the <see cref="IAsyncDisposable"/>.
    /// <para>
    /// This host is an application, but not a Web application: its <see cref="IHostedService"/> do the job.
    /// </para>
    /// </summary>
    public class ServicedApplication : IAsyncDisposable
    {
        readonly IHost _host;

        internal ServicedApplication( IHost host )
        {
            _host = host;
        }

        /// <summary>
        /// Gets the services.
        /// </summary>
        public IServiceProvider Services => _host.Services;

        /// <summary>
        /// Starts the <see cref="IHostedService" /> objects configured for the program.
        /// The application will run until interrupted or until <see cref="M:IHostApplicationLifetime.StopApplication()" /> is called.
        /// </summary>
        /// <param name="cancellationToken">Used to abort program start.</param>
        /// <returns>A <see cref="Task"/> that will be completed when this host started.</returns>
        public virtual Task StartAsync( CancellationToken cancellationToken = default ) => _host.StartAsync( cancellationToken );

        /// <summary>
        /// Attempts to gracefully stop this host.
        /// </summary>
        /// <param name="cancellationToken">Used to indicate when stop should no longer be graceful.</param>
        /// <returns>A <see cref="Task"/> that will be completed when this host stopped.</returns>
        public virtual Task StopAsync( CancellationToken cancellationToken = default ) => _host.StopAsync( cancellationToken );

        /// <summary>
        /// Disposes this host.
        /// </summary>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        public virtual ValueTask DisposeAsync() => ((IAsyncDisposable)_host).DisposeAsync();
    }
}
