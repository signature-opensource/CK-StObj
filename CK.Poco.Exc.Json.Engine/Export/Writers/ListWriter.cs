using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed class ListWriter : JsonCodeWriter
    {
        readonly IPocoType _itemType;

        public ListWriter( ExportCodeWriterMap map, IPocoType itemType )
            : base( map, $"List_{itemType.Index}" )
        {
            _itemType = itemType;
        }

        public override void RawWrite( ICodeWriter writer, string variableName )
        {
            writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write" ).Append( Key!.ToString() ).Append( "( w, " ).Append( variableName ).Append( ", wCtx );" );
        }

        protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                     ICSCodeGenerationContext generationContext,
                                                     ExportCodeWriterMap writers,
                                                     ITypeScope exporterType,
                                                     ITypeScope pocoDirectoryType )
        {
            exporterType.Append( "internal static void Write" ).Append( Key!.ToString() )
                        .Append( "( System.Text.Json.Utf8JsonWriter w, List<" ).Append( _itemType.ImplTypeName )
                        .Append( "> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                        .OpenBlock()
                        .Append( "w.WriteStartArray();" ).NewLine()
                        .Append( "var a = System.Runtime.InteropServices.CollectionsMarshal.AsSpan( v );" ).NewLine()
                        .Append( "for( int i = 0; i < a.Length; ++i )" )
                        .OpenBlock();
            writers.GetWriter( _itemType ).GenerateWrite( exporterType, _itemType, "a[i]" );
            exporterType.CloseBlock()
                        .Append( "w.WriteEndArray();" ).NewLine()
                        .CloseBlock();
        }

    }
}
