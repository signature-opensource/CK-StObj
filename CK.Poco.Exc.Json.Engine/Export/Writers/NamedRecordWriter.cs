using CK.CodeGen;
using CK.Core;

namespace CK.Setup.PocoJson;

sealed class NamedRecordWriter : JsonCodeWriter
{
    readonly IRecordPocoType _record;

    public NamedRecordWriter( ExportCodeWriterMap map, IRecordPocoType record )
        : base( map )
    {
        _record = record;
    }

    public override void RawWrite( ICodeWriter writer, string variableName )
    {
        writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
              .Append( _record.Index )
              .Append( "( w, ref " ).Append( variableName ).Append( ", wCtx );" );
    }

    protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                 ICSCodeGenerationContext generationContext,
                                                 ExportCodeWriterMap writers,
                                                 ITypeScope exporterType,
                                                 ITypeScope pocoDirectoryType )
    {
        exporterType.Append( "internal static void Write_" ).Append( _record.Index )
                    .Append( "(System.Text.Json.Utf8JsonWriter w, ref " )
                    .Append( _record.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                    .OpenBlock()
                    .Append( "w.WriteStartObject();" ).NewLine();
        foreach( var f in _record.Fields )
        {
            if( writers.NameMap.TypeSet.Contains( f.Type ) )
            {
                PocoWriter.GenerateWriteField( exporterType, writers, f, $"v.{f.Name}", requiresCopy: f.Type is IRecordPocoType );
            }
        }
        exporterType.Append( "w.WriteEndObject();" ).NewLine()
                    .CloseBlock();
    }
}
