using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.StObj.Engine.Tests.Endpoint
{
    /// <summary>
    /// This endpoint relies only on the AmbientServiceHub.
    /// Ambient services can be overridden in the hub before using its generated <see cref="IEndpointServiceProvider{TScopeData}.CreateScope(TScopeData)"/>
    /// method.
    /// </summary>
    [EndpointDefinition( EndpointKind.Back )]
    public abstract class BackgroundEndpointDefinition : EndpointDefinition<BackgroundEndpointDefinition.Data>
    {
        public sealed class Data : BackScopedData
        {
            internal Data( AmbientServiceHub ambientServiceHub, IActivityMonitor monitor )
                : base( ambientServiceHub )
            {
                Monitor = monitor;
            }

            internal IActivityMonitor Monitor { get; }

        }

        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider, Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped( sp => scopeData( sp ).Monitor );
            services.AddScoped( sp => scopeData( sp ).Monitor.ParallelLogger );
        }
    }
}
