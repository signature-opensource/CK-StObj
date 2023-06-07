using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.CompilerServices;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    sealed class Fake2EndpointDefinition : EndpointDefinition<Fake2EndpointDefinition.Data>
    {
        public sealed class Data : ScopedData
        {
            internal readonly IActivityMonitor _monitor;

            public Data( EndpointUbiquitousInfo info, IActivityMonitor monitor )
                : base( info )
            {
                _monitor = monitor;
            }
        }

        #region Generated code
        public override string Name => "Fake2";

        public override void ConfigureUbiquitousEndpointInfoServices( IServiceCollection services )
        {
            services.AddScoped( sp => (IFakeAuthenticationInfo)ResolveFromUbiquitous( typeof( IFakeAuthenticationInfo ), sp ) );
            services.AddScoped( sp => (FakeCultureInfo)ResolveFromUbiquitous( typeof( FakeCultureInfo ), sp ) );
            services.AddScoped( sp => (IFakeTenantInfo)ResolveFromUbiquitous( typeof( IFakeTenantInfo ), sp ) );
        }

        static object ResolveFromUbiquitous( Type t, IServiceProvider sp )
        {
            var data = scopeData( sp );
            var map = data.UbiquitousInfo.GetMapping( t );
            if( map is ITuple spMap )
            {
                return ((Func<IServiceProvider, object>)spMap[0]!).Invoke( sp );
            }
            if( map is Delegate )
            {
                // Use a regular cast: we don't control user mismatch of ScopedData. If it's wrong, we want a
                // proper cast exception (not a hard engine crash).
                return ((Func<Data, object>)map).Invoke( data );
            }
            return map;
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
        }

    }
}
