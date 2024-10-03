using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant;

/// <summary>
/// Sample specific singleton service that will be available only from the Fake endpoint.
/// </summary>
public sealed class SpecificSingletonOfTheFakeEndpoint
{
}

[DIContainerDefinition( DIContainerKind.Background )]
abstract class FakeBackDIContainerDefinition : DIContainerDefinition<FakeBackDIContainerDefinition.Data>
{
    // Required definition of the specialized ScopedData type.
    // This can typically define internal fields used to exchange data from the external
    // to the internal world.
    // Here we have decided to explicitly provide the IActivityMonitor. This supposes that
    // this DI container must be used with awaiting whathever it does.
    public sealed class Data : BackendScopedData
    {
        internal readonly IActivityMonitor _monitor;

        public Data( AmbientServiceHub info, IActivityMonitor monitor )
            : base( info )
        {
            _monitor = monitor;
        }
    }

    // This method is implemented by the developer of the Endpoint.
    // The services collection is initially empty but the globalServiceExists can be used
    // to challenge the existence of a service in the global container and adapt the behavior
    // (if you like pain).
    // This enables the endpoint to inject new service types or override registrations of existing
    // services registered in the endpoint container.
    public override void ConfigureContainerServices( IServiceCollection services,
                                                    Func<IServiceProvider,Data> scopeData,
                                                    IServiceProviderIsService globalServiceExists )
    {
        // When registering a monitor, don't forget to register its ParallelLogger.
        services.AddScoped( sp => scopeData( sp )._monitor );
        services.AddScoped( sp => scopeData( sp )._monitor.ParallelLogger );

        services.AddSingleton( new SpecificSingletonOfTheFakeEndpoint() );
    }

}
