using CK.CodeGen;
using CK.Core;
using System;
using System.Numerics;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// The code writer delegate is in charge of generating the write code into a <see cref="System.Text.Json.Utf8JsonWriter"/>
    /// named "w" and a PocoJsonWriteContext variable named "wCtx" from a variable.
    /// </summary>
    /// <param name="writer">The code writer to uses.</param>
    /// <param name="variableName">The variable name to write.</param>
    delegate void CodeWriter( ICodeWriter writer, string variableName );

    sealed partial class ExportCodeGenerator
    {
        readonly ITypeScope _exporterType;
        readonly IPocoTypeNameMap _nameMap;
        readonly ICSCodeGenerationContext _generationContext;
        readonly WriterMap _writerMap;

        public ExportCodeGenerator( ITypeScope exporterType,
                                    IPocoTypeNameMap nameMap,
                                    ICSCodeGenerationContext generationContext )
        {
            _exporterType = exporterType;
            _nameMap = nameMap;
            _generationContext = generationContext;
            _writerMap = new WriterMap( nameMap );
        }

        public bool Run( IActivityMonitor monitor )
        {
            InitializeWriterMap();
            GenerateWriteMethods( monitor );
            GenerateWriteNonNullableFinalType();
            GenerateWriteAny();
            SupportPocoDirectoryJsonExportGenerated( monitor );
            return true;
        }

        void SupportPocoDirectoryJsonExportGenerated( IActivityMonitor monitor )
        {
            ITypeScope pocoDirectory = _generationContext.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, typeof( PocoDirectory ) );
            pocoDirectory.Definition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.IPocoDirectoryJsonExportGenerated" ) );
            var read = pocoDirectory.CreateFunction( "void CK.Core.IPocoDirectoryJsonExportGenerated.WriteAnyJson( " +
                                                        "System.Text.Json.Utf8JsonWriter w, " +
                                                        "object? o, " +
                                                        "Poco.Exc.Json.PocoJsonExportOptions? options)" );

            read.Append( "if( o == null ) w.WriteNullValue();" ).NewLine()
                .Append( "else" )
                .OpenBlock()
                .Append( "var wCtx = new CK.Poco.Exc.Json.PocoJsonWriteContext( this, options );" ).NewLine()
                .Append( _exporterType.FullName ).Append( ".WriteAny( w, o, wCtx );" )
                .CloseBlock();
        }

    }
}
