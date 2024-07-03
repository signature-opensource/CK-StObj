using CK.CodeGen;
using CK.Core;

namespace CK.Setup.PocoJson
{
    sealed partial class ExportCodeGenerator
    {
        readonly ITypeScope _exporterType;
        readonly IPocoTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;

        public ExportCodeGenerator( ITypeScope exporterType,
                                    IPocoTypeNameMap nameMap,
                                    ICSCodeGenerationContext generationContext )
        {
            _exporterType = exporterType;
            _nameMap = nameMap;
            _generationContext = generationContext;
        }

        public bool Run( IActivityMonitor monitor )
        {
            ITypeScope pocoDirectory = _generationContext.GeneratedCode.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
            SupportPocoDirectoryJsonExportGenerated( pocoDirectory );
            var writerMap = new JsonCodeWriterMap( _nameMap );
            return writerMap.Run( monitor, _generationContext, _exporterType, pocoDirectory );
        }

        void SupportPocoDirectoryJsonExportGenerated( ITypeScope pocoDirectory )
        {
            pocoDirectory.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.IPocoDirectoryJsonExportGenerated" ) );
            var wAny = pocoDirectory
                            .CreateFunction( "bool CK.Core.IPocoDirectoryJsonExportGenerated.WriteAnyJson( " +
                                                        "System.Text.Json.Utf8JsonWriter w, " +
                                                        "object? o, " +
                                                        "Poco.Exc.Json.PocoJsonWriteContext wCtx )" );

            wAny.GeneratedByComment()
                .Append( "return " ).Append( _exporterType.FullName ).Append(".WriteAny( w, o, wCtx );" );

            var wPoco = pocoDirectory
                            .CreateFunction( "bool CK.Core.IPocoDirectoryJsonExportGenerated.WriteJson( " +
                                                        "System.Text.Json.Utf8JsonWriter w, " +
                                                        "IPoco? o," +
                                                        "Poco.Exc.Json.PocoJsonWriteContext wCtx, " +
                                                        "bool withType )" );

            wPoco.GeneratedByComment()
                .Append( """
                    if( o == null )
                    {
                        w.WriteNullValue();
                        return true;
                    }
                    return System.Runtime.CompilerServices.Unsafe.As<PocoJsonExportSupport.IWriter>( o ).WriteJson( w, wCtx, withType );
                    """ );
        }

    }
}
