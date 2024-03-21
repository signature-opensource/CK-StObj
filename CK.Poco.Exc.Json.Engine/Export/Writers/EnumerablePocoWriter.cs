using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed class EnumerablePocoWriter : JsonCodeWriter
    {
        public EnumerablePocoWriter( ExportCodeWriterMap map )
            : base( map, nameof( EnumerablePocoWriter ) )
        {
        }

        public override void RawWrite( ICodeWriter writer, string variableName )
        {
            writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteEnumerablePoco( w, " ).Append( variableName ).Append( ", wCtx );" );
        }

        protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                     ICSCodeGenerationContext generationContext,
                                                     ExportCodeWriterMap writers,
                                                     ITypeScope exporterType,
                                                     ITypeScope pocoDirectoryType )
        {
            exporterType.Append( """
                internal static void WriteEnumerablePoco( System.Text.Json.Utf8JsonWriter w, IEnumerable<IPoco> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
                {
                   w.WriteStartArray();
                   foreach( var e in v )
                   {
                      System.Runtime.CompilerServices.Unsafe.As<PocoJsonExportSupport.IWriter>( e ).WriteJson( w, wCtx );
                   }
                   w.WriteEndArray();
                }
                
                """ );
        }
    }

}
