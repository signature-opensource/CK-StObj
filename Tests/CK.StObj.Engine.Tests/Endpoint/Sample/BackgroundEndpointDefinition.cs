using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [EndpointDefinition]
    public abstract class BackgroundEndpointDefinition : EndpointDefinition<BackgroundEndpointDefinition.Data>
    {
        public sealed class Data : ScopedData
        {
            internal Data( EndpointUbiquitousInfo ubiquitousInfo, IActivityMonitor monitor )
                : base( ubiquitousInfo )
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
