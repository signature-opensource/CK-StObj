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
        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext codeGenContext )
        {
            var typeSystem = codeGenContext.CurrentRun.ServiceContainer.GetRequiredService<IPocoTypeSystem>();
            // Ensures that byte object array and List are registered (there's no reason they couldn't, hence the Throw).
            Throw.CheckState( typeSystem.RegisterNullOblivious( monitor, typeof( List<object> ) ) != null );
            Throw.CheckState( typeSystem.RegisterNullOblivious( monitor, typeof( object[] ) ) != null );
            return new CSCodeGenerationResult( nameof( WaitForLockedTypeSystem ) );
        }

        CSCodeGenerationResult WaitForLockedTypeSystem( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystem typeSystem )
        {
            if( typeSystem.IsLocked )
            {
                return new CSCodeGenerationResult( nameof( WaitForLockedTypeSystem ) );
            }
            monitor.Trace( $"PocoTypeSystem is locked. Json code generation can start." );
            return new CSCodeGenerationResult( nameof( GenerateAllCode ) );
        }

        CSCodeGenerationResult GenerateAllCode( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystem typeSystem )
        {
            var nameMap = new ExchangeableTypeNameBuilder().Generate( monitor, typeSystem, false );

            var ns = c.Assembly.Code.Global.FindOrCreateNamespace( "CK.Poco.Exc.JsonGen" );

            var exporterType = ns.CreateType( "internal static class Exporter" );
            var export = new ExportCodeGenerator( exporterType, nameMap, c );
            if( !export.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            var importerType = ns.CreateType( "internal static class Importer" );
            var import = new ImportCodeGenerator( importerType, nameMap, c );
            if( !import.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            return CSCodeGenerationResult.Success;
        }
    }
}
