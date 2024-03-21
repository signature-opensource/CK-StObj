using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed class ArrayWriter : JsonCodeWriter
    {
        readonly ExportCodeWriter _itemWriter;
        readonly IPocoType _itemSample;

        public ArrayWriter( ExportCodeWriterMap map, ExportCodeWriter itemWriter, IPocoType itemSample )
            : base( map, $"Array_{itemWriter.Index}" )
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
                        .Append( "( System.Text.Json.Utf8JsonWriter w, " ).Append( _itemSample.ImplTypeName )
                        .Append( "[] v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                        .OpenBlock()
                        .Append( "w.WriteStartArray();" ).NewLine()
                        .Append( "var a = v.AsSpan();" ).NewLine()
                        .Append( "for( int i = 0; i < a.Length; ++i )" )
                        .OpenBlock();
            _itemWriter.GenerateWrite( exporterType, _itemSample, "a[i]" );
            exporterType.CloseBlock()
                        .Append( "w.WriteEndArray();" ).NewLine()
                        .CloseBlock();
        }

    }

}
