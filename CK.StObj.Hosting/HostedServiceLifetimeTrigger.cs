using CK.Core;
using CK.Setup;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CK.StObj.Hosting
{
    /// <summary>
    /// Automatic singleton hosted service that calls OnHostStartAsync and OnHostStopAsync private methods
    /// of real objects.
    /// </summary>
    [ContextBoundDelegation( "CK.StObj.Hosting.Engine.HostedServiceLifetimeTriggerImpl, CK.StObj.Hosting.Engine" )]
    public abstract class HostedServiceLifetimeTrigger : IHostedService, ISingletonAutoService
    {
        /// <summary>
        /// Holds the name 'OnHostStartAsync'.
        /// This must be a non virtual and private Task or ValueTask returning method with parameters that can be any singleton or scoped services 
        /// (a dedicated scope is created for the call, scoped services won't pollute the application services).
        /// </summary>
        public static readonly string StartMethodNameAsync = "OnHostStartAsync";

        /// <summary>
        /// Holds the name 'OnHostStart'.
        /// This must be a non virtual, typically private void method with parameters that can be any singleton or scoped services 
        /// (a dedicated scope is created for the call, scoped services won't pollute the application services).
        /// </summary>
        public static readonly string StartMethodName = "OnHostStart";

        /// <summary>
        /// Holds the name 'OnHostStopAsync'. Same as the <see cref="StartMethodNameAsync"/>.
        /// </summary>
        public static readonly string StopMethodNameAsync = "OnHostStopAsync";

        /// <summary>
        /// Holds the name 'OnHostStop'. Same as the <see cref="StopMethodNameAsync"/>.
        /// </summary>
        public static readonly string StopMethodName = "OnHostStop";

        /// <summary>
        /// Calls all existing <see cref="StartMethodNameAsync"/> and <see cref="StartMethodName"/> private methods.
        /// This method implementation is automatically generated.
        /// </summary>
        /// <param name="cancel">The cancellation token.</param>
        /// <returns>The awaitable.</returns>
        public abstract Task StartAsync( CancellationToken cancel );

        /// <summary>
        /// Calls all existing <see cref="StopMethodNameAsync"/> and <see cref="StopMethodName"/> private methods.
        /// This method implementation is automatically generated.
        /// </summary>
        /// <param name="cancel">The cancellation token.</param>
        /// <returns>The awaitable.</returns>
        public abstract Task StopAsync( CancellationToken cancel );

    }
}
