using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson;

sealed class HashSetWriter : JsonCodeWriter
{
    readonly IPocoType _itemType;

    public HashSetWriter( ExportCodeWriterMap map, IPocoType itemType )
        : base( map, $"Set_{itemType.Index}" )
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
                    .Append( "( System.Text.Json.Utf8JsonWriter w, HashSet<" ).Append( _itemType.ImplTypeName )
                    .Append( "> v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                    .OpenBlock()
                    .Append( "w.WriteStartArray();" ).NewLine()
                    .Append( "foreach( var item in v )" )
                    .OpenBlock();
        if( _itemType is IRecordPocoType )
        {
            exporterType.Append( "var loc = item;" ).NewLine();
            writers.GetWriter( _itemType ).GenerateWrite( exporterType, _itemType, "loc" );
        }
        else
        {
            writers.GetWriter( _itemType ).GenerateWrite( exporterType, _itemType, "item" );
        }
        exporterType.CloseBlock()
                    .Append( "w.WriteEndArray();" ).NewLine()
                    .CloseBlock();
    }

}
