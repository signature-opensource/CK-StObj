using CK.CodeGen;
using CK.Core;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Setup.PocoJson;

/// <summary>
/// Implements CK.Poco.Exc.JsonGen.Exporter and Importer static classes.
/// </summary>
public sealed class CommonImpl : ICSCodeGenerator
{
    CSCodeGenerationResult ICSCodeGenerator.Implement( IActivityMonitor monitor, ICSCodeGenerationContext ctx )
    {
        // We can skip any serialization code generation if this run in the Pure Unified.
        if( ctx.CurrentRun.ConfigurationGroup.IsUnifiedPure )
        {
            return CSCodeGenerationResult.Success;
        }
        // Wait for the IPocoSerializableServiceEngine to be available in the curren run.
        return new CSCodeGenerationResult( nameof( WaitForPocoSerializableServiceEngine ) );
    }

    sealed class ServiceEngine : IPocoJsonSerializationServiceEngine
    {
        readonly IPocoSerializationServiceEngine _s;
        readonly ITypeScope _exporter;
        readonly ITypeScope _importer;

        public ServiceEngine( IPocoSerializationServiceEngine s, ITypeScope exporter, ITypeScope importer )
        {
            _s = s;
            _exporter = exporter;
            _importer = importer;
        }

        public IPocoSerializationServiceEngine SerializableLayer => _s;

        public ITypeScope Exporter => _exporter;

        public ITypeScope Importer => _importer;
    }

    CSCodeGenerationResult WaitForPocoSerializableServiceEngine( IActivityMonitor monitor, ICSCodeGenerationContext c )
    {
        var s = c.CurrentRun.ServiceContainer.GetService<IPocoSerializationServiceEngine>();
        if( s == null )
        {
            return new CSCodeGenerationResult( nameof( WaitForPocoSerializableServiceEngine ) );
        }
        using( monitor.OpenInfo( $"IPocoSerializableServiceEngine is available. Starting Json serialization code generation." ) )
        {
            var ns = c.Assembly.Code.Global.FindOrCreateNamespace( "CK.Poco.Exc.JsonGen" );

            var exporterType = ns.CreateType( "internal static class Exporter" );
            var export = new ExportCodeGenerator( exporterType, s.SerializableNames, c );
            if( !export.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            var importerType = ns.CreateType( "internal static class Importer" );
            var import = new ImportCodeGenerator( importerType, s.SerializableNames, c );
            if( !import.Run( monitor ) ) return CSCodeGenerationResult.Failed;

            // Expose the associated service for the others.
            var exposedService = new ServiceEngine( s, exporterType, importerType );
            c.CurrentRun.ServiceContainer.Add<IPocoJsonSerializationServiceEngine>( exposedService );
        }
        return CSCodeGenerationResult.Success;
    }

}
