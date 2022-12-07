using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace CK.Setup.PocoJson
{
    public sealed class CommonImpl : ICSCodeGenerator
    {
        // We must give a chance to other generators to register new types (typically the result of Cris ICOmmand<TResult>).
        // We have to wait for all the PocoType to be registered, but how do we know that other generators are done with all
        // their required registrations?
        // Instead of doing one hop on the trampoline and prey that no one will need more, we continue hopping until no
        // more new registrations are done.
        int _lastRegistrationCount;

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var typeSystem = codeGenContext.CurrentRun.ServiceContainer.GetRequiredService<IPocoTypeSystem>();
            // Ensures that byte object array and List are registered (there's no reason they couldn't, hence the Throw).
            Throw.CheckState( typeSystem.RegisterNullOblivious( monitor, typeof( List<object> ) ) != null );
            Throw.CheckState( typeSystem.RegisterNullOblivious( monitor, typeof( object[] ) ) != null );
            // Catches the current registration count.
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
            var nameMap = new ExchangeableTypeNameBuilder().Generate( monitor, typeSystem, false );
            var simplifieldNameMap = new JsonTypeSimplifiedNameBuilder().Generate( monitor, typeSystem, false, "*" );

            var ns = c.Assembly.Code.Global.FindOrCreateNamespace( "CK.Poco.Exc.JsonGen" );

            var exporterType = ns.CreateType( "internal static class Exporter" );
            var export = new ExportCodeGenerator( exporterType, nameMap, simplifieldNameMap, c );
            if( !export.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            var importerType = ns.CreateType( "internal static class Importer" );
            var import = new ImportCodeGenerator( importerType, nameMap, c );
            if( !import.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            return CSCodeGenerationResult.Success;
        }
    }
}
