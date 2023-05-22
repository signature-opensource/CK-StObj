using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Setup
{
    partial class StObjCollector
    {
        void AddWellKnownServices( IActivityMonitor monitor )
        {
            Debug.Assert( !_wellKnownServiceKindRegistered );
            _wellKnownServiceKindRegistered = true;

            SetAutoServiceKind( monitor, typeof( IActivityMonitor ), AutoServiceKind.IsEndpointService | AutoServiceKind.IsScoped );

            // The IServiceProvider itself is a Singleton.   
            SetAutoServiceKind( monitor, typeof( IServiceProvider ), AutoServiceKind.IsSingleton );

            // Registration must be done from the most specific types to the basic ones: here we must
            // start with IOptionsSnapshot since IOptionsSnapshot<T> extends IOptions<T>.
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Options.IOptionsSnapshot`1, Microsoft.Extensions.Options", AutoServiceKind.IsScoped | AutoServiceKind.IsProcessService, isOptional: true );
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Options.IOptions`1, Microsoft.Extensions.Options", AutoServiceKind.IsSingleton | AutoServiceKind.IsProcessService, isOptional: true );
            // IOptionsMonitor is independent.
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Options.IOptionsMonitor`1, Microsoft.Extensions.Options", AutoServiceKind.IsSingleton | AutoServiceKind.IsProcessService, isOptional: true );

            // This defines a  [Multiple] ISingletonAutoService. Thanks to this definition, hosted services implementations that are IAutoServices are automatically registered.
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions", AutoServiceKind.IsSingleton | AutoServiceKind.IsMultipleService, isOptional: true );

            // Other well known services life time can be defined...
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Logging.ILoggerProvider, Microsoft.Extensions.Logging.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
            // The IHostEnvironment is a singleton (tied to the process, not marshallable).
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Hosting.IHostEnvironment, Microsoft.Extensions.Hosting.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );

            // Other known singletons.
            SetAutoServiceKind( monitor, "System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", AutoServiceKind.IsSingleton, isOptional: true );
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Configuration.IConfigurationRoot, Microsoft.Extensions.Configuration.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
            // IConfigurationRoot is not added to the DI by default, only the IConfiguration (that happens to be the root) is registered.
            // See https://github.com/aspnet/templating/issues/193#issuecomment-351137277.
            SetAutoServiceKind( monitor, "Microsoft.Extensions.Configuration.IConfiguration, Microsoft.Extensions.Configuration.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );

            // The SignalR IHubContext<THub> and IHubContext<THub,T> are singletons (that expose all the Clients of the hub).
            SetAutoServiceKind( monitor, "Microsoft.AspNetCore.SignalR.IHubContext`1, Microsoft.AspNetCore.SignalR.Core", AutoServiceKind.IsSingleton, isOptional: true );
            SetAutoServiceKind( monitor, "Microsoft.AspNetCore.SignalR.IHubContext`2, Microsoft.AspNetCore.SignalR.Core", AutoServiceKind.IsSingleton, isOptional: true );
        }
    }
}
