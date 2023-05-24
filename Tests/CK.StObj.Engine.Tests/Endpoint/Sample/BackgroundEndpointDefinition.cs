using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace CK.StObj.Engine.Tests.Endpoint
{
    public interface IBackgroundEndpointType : IEndpointType<BackgroundEndpointDefinition.BackgroundData> { }

    [EndpointDefinition]
    public abstract class BackgroundEndpointDefinition : EndpointDefinition<BackgroundEndpointDefinition.BackgroundData>
    {
        public sealed class BackgroundData
        {
            internal BackgroundData( ActivityMonitor monitor )
            {
                Monitor = monitor;
            }

            internal IActivityMonitor Monitor { get; }

            [AllowNull]
            internal IFakeAuthenticationInfo Auth { get; set; }
        }

        public override void ConfigureEndpointServices( IServiceCollection services )
        {
            services.AddScoped( sp => sp.GetRequiredService<BackgroundData>().Monitor );
            services.AddScoped( sp => sp.GetRequiredService<BackgroundData>().Auth );
        }
    }
}
