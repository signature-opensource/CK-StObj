using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

[TestFixture]
public class OnHostStartStopTests
{
    public interface ISqlCallContext : IScopedAutoService
    {
        bool Disposed { get; }
    }

    public class SqlStandardCallContext : ISqlCallContext, IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    public interface IMailerService : ISingletonAutoService
    {
        void SendMail( IActivityMonitor monitor, string mail );
    }

    public class MailerService : IMailerService
    {
        public void SendMail( IActivityMonitor monitor, string mail )
        {
            monitor.Info( $"Sending mail: '{mail}'." );
        }

        public void Shutdown( IActivityMonitor monitor, ISqlCallContext ctx )
        {
            ctx.Disposed.ShouldBeFalse();
            monitor.Info( "Mail Service Shutdown." );
        }
    }

    public class O1 : IRealObject
    {
        void OnHostStart( IActivityMonitor m, ISqlCallContext ctx )
        {
            ctx.Disposed.ShouldBeFalse();
            m.Info( $"O1 is starting." );
        }

        void OnHostStop( IActivityMonitor m, ISqlCallContext ctx, CancellationToken cancel )
        {
            ctx.Disposed.ShouldBeFalse();
            m.Info( $"O1 is stopping." );
        }
    }

    public class O1Spec : O1
    {
        ValueTask OnHostStartAsync( IActivityMonitor m, CancellationToken cancel )
        {
            m.Info( $"O1Spec is starting." );
            return default;
        }

        ValueTask OnHostStopAsync( IActivityMonitor m, ISqlCallContext ctx, CancellationToken cancel )
        {
            ctx.Disposed.ShouldBeFalse();
            m.Info( $"O1Spec is stopping." );
            return default;
        }
    }

    public class O2 : IRealObject
    {
        void StObjConstruct( O1 o1 ) { }
    }

    public class O2Spec : O2
    {
        void StObjConstruct( O1Spec o1Spec ) { }

        Task OnHostStartAsync( IActivityMonitor m )
        {
            m.Info( $"O2Spec is starting." );
            return Task.CompletedTask;
        }

        Task OnHostStopAsync( IActivityMonitor m, ISqlCallContext ctx, MailerService mail )
        {
            m.Info( $"O2Spec is stopping." );
            mail.Shutdown( m, ctx );
            return Task.CompletedTask;
        }
    }


    [Test]
    public async Task HostedServiceLifetimeTrigger_at_work_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( OnHostStartStopTests ).GetNestedTypes() );
        await using var auto = (await configuration.RunAsync().ConfigureAwait(false)).CreateAutomaticServices( configureServices: services =>
        {
            services.AddScoped( sp => TestHelper.Monitor );
            services.AddScoped( sp => TestHelper.Monitor.ParallelLogger );
        } );
        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Info ) )
        {
            var initializers = auto.Services.GetRequiredService<IEnumerable<Microsoft.Extensions.Hosting.IHostedService>>();
            foreach( var i in initializers )
            {
                await i.StartAsync( default );
                await i.StopAsync( default );
            }
            entries.Select( e => e.Text ).Concatenate( "|" )
                .ShouldBe( "Calling: 1 'OnHostStart' method and 2 'OnHostStartAsync' methods.|O1 is starting.|O1Spec is starting.|O2Spec is starting."
                              + "|Calling: 2 'OnHostStopAsync' methods and 1 'OnHostStop' method.|O2Spec is stopping.|Mail Service Shutdown.|O1Spec is stopping.|O1 is stopping." );
        }
    }
}
