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
    public sealed class CommonImpl : ICSCodeGenerator, IPocoJsonGeneratorService
    {
        IPocoTypeSystemBuilder? _pocoTypeSystem;
        PocoTypeNameMap? _nameMap;

        public CSCodeGenerationResult Implement( IActivityMonitor monitor, ICSCodeGenerationContext ctx )
        {
            _pocoTypeSystem = ctx.CurrentRun.ServiceContainer.GetRequiredService<IPocoTypeSystemBuilder>();
            // Wait for the type system to be locked.
            return new CSCodeGenerationResult( nameof( WaitForLockedTypeSystem ) );
        }

        CSCodeGenerationResult WaitForLockedTypeSystem( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystemBuilder typeSystemBuilder )
        {
            if( !typeSystemBuilder.IsLocked )
            {
                return new CSCodeGenerationResult( nameof( WaitForLockedTypeSystem ) );
            }
            monitor.Trace( $"PocoTypeSystemBuilder is locked. Json serialization code generation can start." );
            return new CSCodeGenerationResult( nameof( GenerateAllCode ) );
        }

        CSCodeGenerationResult GenerateAllCode( IActivityMonitor monitor, ICSCodeGenerationContext c, IPocoTypeSystem typeSystem )
        {
            _nameMap = new PocoTypeNameMap( typeSystem.SetManager.AllSerializable );
            // Now that the map is available, registers this IPocoJsonService for others.
            c.CurrentRun.ServiceContainer.Add<IPocoJsonGeneratorService>( this );

            var ns = c.Assembly.Code.Global.FindOrCreateNamespace( "CK.Poco.Exc.JsonGen" );

            var exporterType = ns.CreateType( "internal static class Exporter" );
            var export = new ExportCodeGenerator( exporterType, _nameMap, c );
            if( !export.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            var importerType = ns.CreateType( "internal static class Importer" );
            var import = new ImportCodeGenerator( importerType, _nameMap, c );
            if( !import.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            return CSCodeGenerationResult.Success;
        }

        PocoTypeNameMap IPocoJsonGeneratorService.JsonNames => _nameMap!;

    }
}
