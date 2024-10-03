using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson;

sealed class EnumerablePolymorphicNullWriter : JsonCodeWriter
{
    public EnumerablePolymorphicNullWriter( ExportCodeWriterMap map )
        : base( map, nameof( EnumerablePolymorphicNullWriter ) )
    {
    }

    public override void RawWrite( ICodeWriter writer, string variableName )
    {
        writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteEnumerablePolymorphicNull( w, " ).Append( variableName ).Append( ", wCtx );" );
    }

    protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                 ICSCodeGenerationContext generationContext,
                                                 ExportCodeWriterMap writers,
                                                 ITypeScope exporterType,
                                                 ITypeScope pocoDirectoryType )
    {
        exporterType.Append( """
            internal static void WriteEnumerablePolymorphicNull( System.Text.Json.Utf8JsonWriter w, IEnumerable<object> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
            {
               w.WriteStartArray();
               foreach( var e in v )
               {
                  if( e == null ) w.WriteNullValue();
                  else CK.Poco.Exc.JsonGen.Exporter.WriteAny( w, e, wCtx );
               }
               w.WriteEndArray();
            }
            
            """ );
    }
}
