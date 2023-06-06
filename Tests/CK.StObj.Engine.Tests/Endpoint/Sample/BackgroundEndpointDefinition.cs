using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;

namespace CK.StObj.Engine.Tests.Endpoint
{
    [EndpointDefinition]
    public abstract class BackgroundEndpointDefinition : EndpointDefinition<BackgroundEndpointDefinition.BackgroundData>
    {
        public sealed class BackgroundData : ScopedData
        {
            internal BackgroundData( IEndpointType<BackgroundData> _endpoint, IActivityMonitor monitor, IFakeAuthenticationInfo auth )
                : base( null )
            {
                Monitor = monitor;
                Auth = auth;
            }

            internal IActivityMonitor Monitor { get; }

            [AllowNull]
            internal IFakeAuthenticationInfo Auth { get; }
        }

        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider, BackgroundData> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped( sp => scopeData( sp ).Monitor );
            services.AddScoped( sp => scopeData( sp ).Monitor.ParallelLogger );
            services.AddScoped( sp => scopeData( sp ).Auth );
        }
    }
}
