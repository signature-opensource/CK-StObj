using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace CK.Setup.PocoJson
{
    public sealed class CommonImpl : CSCodeGeneratorType
    {
        // We must give a chance to other generators to register new types (typically the result of Cris ICOmmand<TResult>).
        // We have to wait for all the PocoType to be registered, but how do we know that other generators are done with all
        // their required registrations?
        // Instead of doing one hop on the trampoline and prey that no one will need more, we continue hopping until no
        // more new registrations are done.
        int _lastRegistrationCount;

        public override CSCodeGenerationResult Implement( IActivityMonitor monitor, Type classType, ICSCodeGenerationContext c, ITypeScope scope )
        {
            var typeSystem = c.CurrentRun.ServiceContainer.GetRequiredService<IPocoTypeSystem>();
            _lastRegistrationCount = typeSystem.AllTypes.Count;
            monitor.Trace( $"PocoTypeSystem has initially {_lastRegistrationCount} registered types." );
            return new CSCodeGenerationResult( nameof( CheckNoMoreRegisteredPocoTypes ) );
        }

        CSCodeGenerationResult CheckNoMoreRegisteredPocoTypes( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystem typeSystem )
        {
            var newCount = typeSystem.AllTypes.Count;
            if( newCount != _lastRegistrationCount )
            {
                monitor.Trace( $"PocoTypeSystem has {newCount - _lastRegistrationCount} new types. Deferring the Json code generation step." );
                _lastRegistrationCount = newCount;
                return new CSCodeGenerationResult( nameof( CheckNoMoreRegisteredPocoTypes ) );
            }
            monitor.Trace( $"PocoTypeSystem has no new types, Json code generation can start." );
            return new CSCodeGenerationResult( nameof( GenerateAllCode ) );
        }

        CSCodeGenerationResult GenerateAllCode( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystem typeSystem )
        {
            var exchangedNames = new JsonTypeNameBuilder().Generate( monitor, typeSystem, false );

            var pocoDirectory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );

            var export = new ExportCodeGenerator( pocoDirectory, typeSystem, c );
            if( !export.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            var import = new ImportCodeGenerator( pocoDirectory, typeSystem, c );
            if( !import.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            return CSCodeGenerationResult.Success;
        }
    }
}
