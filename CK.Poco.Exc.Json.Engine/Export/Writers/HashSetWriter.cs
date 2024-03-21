using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed class HashSetWriter : JsonCodeWriter
    {
        readonly ExportCodeWriter _itemWriter;
        readonly IPocoType _itemSample;

        public HashSetWriter( ExportCodeWriterMap map, ExportCodeWriter itemWriter, IPocoType itemSample )
            : base( map, $"Set_{itemWriter.Index}" )
        {
            _itemWriter = itemWriter;
            _itemSample = itemSample;
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
                        .Append( "( System.Text.Json.Utf8JsonWriter w, HashSet<" ).Append( _itemSample.ImplTypeName )
                        .Append( "> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                        .OpenBlock()
                        .Append( "w.WriteStartArray();" ).NewLine()
                        .Append( "var a = System.Runtime.InteropServices.CollectionsMarshal.AsSpan( v );" ).NewLine()
                        .Append( "foreach( var item in v )" )
                        .OpenBlock();
            if( _itemSample is IRecordPocoType )
            {
                exporterType.Append( "var loc = item;" ).NewLine();
                _itemWriter.GenerateWrite( exporterType, _itemSample, "loc" );
            }
            else
            {
                _itemWriter.GenerateWrite( exporterType, _itemSample, "item" );
            }
            exporterType.CloseBlock()
                        .Append( "w.WriteEndArray();" ).NewLine()
                        .CloseBlock();
        }

    }
}
