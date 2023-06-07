using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.CompilerServices;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    /// <summary>
    /// Sample specific singleton service that will be available only from the Fake endpoint.
    /// </summary>
    public sealed class SpecificSingletonOfTheFakeEndpoint { }

    sealed class FakeEndpointDefinition : EndpointDefinition<FakeEndpointDefinition.Data>
    {
        // Required definition of the specialized ScopedData type.
        // This can typically define internal fields used to exchange data from the external
        // to the internal world.
        // Here we have decided to explicitly provide the IActivityMonitor. This supposes that
        // it is "reserved" for work on this side!
        // We can do the same for a ubiquitous service like the IFakeAuthenticationInfo BUT
        // this logically ties this endpoint to this service (its package): this endpoint is no
        // more "neutral", its API is impacted because an instance of the IFakeAuthenticationInfo
        // MUST be provided and eventually reaches this point.
        // To do this, the ubiquitous type MUST appear among the Data constructor parameters:
        // these types are analyzed and registered as being directly handled by the endpoint ConfigureServices.
        // This enables us to skip the standard configuration in the EndpointType<TScopeData>.ConfigureServices.
        public sealed class Data : ScopedData
        {
            internal readonly IActivityMonitor _monitor;
            internal readonly IFakeAuthenticationInfo _auth;

            public Data( EndpointUbiquitousInfo info, IActivityMonitor monitor, IFakeAuthenticationInfo auth )
                : base( info )
            {
                _monitor = monitor;
                _auth = auth;
            }
        }

        #region Generated code
        public override string Name => "Fake";

        // Code generated because we know all the endpoint ubiquitous scoped service thanks
        // to the EndpointScopedServiceAttribute( bool isUbiquitousEndpointInfo = true ).
        // So we know that the ubiquitous services are: IFakeAuthenticationInfo, ICurrentTenantInfo and ICurrentCultureInfo
        // types to be provided by the caller (be it from this process, from another endpoint and/or overridden).
        //
        // And this is not the same for all endpoint definition because of the specific Data type and the fact that
        // we can skip registrations based on the Data constructor's parameters.
        public override void ConfigureUbiquitousEndpointInfoServices( IServiceCollection services )
        {
            // We know that the IFakeAuthenticationInfo must be handled by the manual ConfigureServices because it appears in the Data constructor parameters.
            //services.AddScoped( sp => (IFakeAuthenticationInfo)ResolveFromUbiquitous( typeof( IFakeAuthenticationInfo ), sp ) );
            services.AddScoped( sp => (FakeCultureInfo)EndpointType<Data>.ResolveFromUbiquitous( typeof( FakeCultureInfo ), sp ) );
            services.AddScoped( sp => (IFakeTenantInfo)ResolveFromUbiquitous( typeof( IFakeTenantInfo ), sp ) );
        }

        #endregion

        // This method is implemented by the developer of the Endpoint.
        // The services collection only contains the work of the code generated ConfigureUbiquitousEndpointInfoServices
        // but the globalServiceExists can be used to challenge the existence of a service in the global container
        // and adapt the behavior (if you like pain).
        // This enables the endpoint to inject new service types or override registrations of existing
        // services registered in the endpoint container.
        public override void ConfigureEndpointServices( IServiceCollection services,
                                                        Func<IServiceProvider,Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            // When registering a monitor, don't forget to register its ParallelLogger.
            services.AddScoped( sp => scopeData( sp )._monitor );
            services.AddScoped( sp => scopeData( sp )._monitor.ParallelLogger );

            services.AddScoped( sp => scopeData( sp )._auth );
            services.AddSingleton( new SpecificSingletonOfTheFakeEndpoint() );
        }

    }
}
