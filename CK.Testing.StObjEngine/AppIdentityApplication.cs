using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Threading;
using CK.AppIdentity;

namespace CK.Testing;

/// <summary>
/// Specialized <see cref="ServicedApplication"/> that is returned by CreateSevicedApplication methods
/// when a <see cref="ApplicationIdentityService"/> is available in the services.
/// </summary>
public sealed class AppIdentityApplication : ServicedApplication
{
    readonly ApplicationIdentityService _appIdentity;

    internal AppIdentityApplication( IHost host, ApplicationIdentityService appIdentity )
        : base( host )
    {
        _appIdentity = appIdentity;
    }

    /// <summary>
    /// Gets the <see cref="ApplicationIdentityService"/>.
    /// </summary>
    public ApplicationIdentityService ApplicationIdentityService => _appIdentity;

    /// <summary>
    /// Overridden to wait for the <see cref="ApplicationIdentityService.InitializationTask"/>.
    /// </summary>
    /// <param name="cancellationToken">Used to abort program start.</param>
    /// <returns>A <see cref="Task"/> that will be completed when this AppIdentityApplication started.</returns>
    public override async Task StartAsync( CancellationToken cancellationToken = default )
    {
        var s = ApplicationIdentityService;
        await base.StartAsync( cancellationToken );
        await s.InitializationTask.WaitAsync( cancellationToken );
    }

}
