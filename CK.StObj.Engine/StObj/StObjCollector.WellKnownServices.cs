using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    partial class StObjCollector
    {
        void AddWellKnownServices()
        {
            // The IActivityMobitor is by design a scoped service. It is not Optional (since it necessarily exists).
            SetAutoServiceKind( typeof( IActivityMonitor ), AutoServiceKind.IsScoped );

            // The IServiceProvider itself is a Singleton.   
            SetAutoServiceKind( typeof( IServiceProvider ), AutoServiceKind.IsSingleton );

            // Registration must be done from the most specific types to the basic ones: here we must
            // start with IOptionsSnapshot since IOptionsSnapshot<T> extends IOptions<T>.
            SetAutoServiceKind( "Microsoft.Extensions.Options.IOptionsSnapshot`1, Microsoft.Extensions.Options", AutoServiceKind.IsScoped | AutoServiceKind.IsFrontProcessService, isOptional: true );
            SetAutoServiceKind( "Microsoft.Extensions.Options.IOptions`1, Microsoft.Extensions.Options", AutoServiceKind.IsSingleton | AutoServiceKind.IsFrontProcessService, isOptional: true );
            // IOptionsMonitor is independent.
            SetAutoServiceKind( "Microsoft.Extensions.Options.IOptionsMonitor`1, Microsoft.Extensions.Options", AutoServiceKind.IsSingleton | AutoServiceKind.IsFrontProcessService, isOptional: true );

            // This defines a  [Multiple] ISingletonAutoService. Thanks to this definition, hosted services implementations that are IAutoServices are automatically registered.
            SetAutoServiceKind( "Microsoft.Extensions.Hosting.IHostedService, Microsoft.Extensions.Hosting.Abstractions", AutoServiceKind.IsSingleton | AutoServiceKind.IsMultipleService, isOptional: true );

            // Other well known services life time can be defined...
            SetAutoServiceKind( "Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
            SetAutoServiceKind( "Microsoft.Extensions.Logging.ILoggerProvider, Microsoft.Extensions.Logging.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
            // The IHostEnvironment is a singleton (tied to the process, not marshallable).
            SetAutoServiceKind( "Microsoft.Extensions.Hosting.IHostEnvironment, Microsoft.Extensions.Hosting.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );

            // Other known singletons.
            SetAutoServiceKind( "System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", AutoServiceKind.IsSingleton, isOptional: true );
            SetAutoServiceKind( "Microsoft.Extensions.Configuration.IConfigurationRoot, Microsoft.Extensions.Configuration.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
            // IConfigurationRoot is not added to the DI by default, only the IConfiguration (that happens to be the root) is registered.
            // See https://github.com/aspnet/templating/issues/193#issuecomment-351137277.
            SetAutoServiceKind( "Microsoft.Extensions.Configuration.IConfiguration, Microsoft.Extensions.Configuration.Abstractions", AutoServiceKind.IsSingleton, isOptional: true );
        }
    }
}
