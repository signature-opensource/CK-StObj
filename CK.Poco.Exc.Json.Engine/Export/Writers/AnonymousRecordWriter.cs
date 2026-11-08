using CK.CodeGen;
using CK.Core;

namespace CK.Setup.PocoJson;

sealed class AnonymousRecordWriter : JsonCodeWriter
{
    readonly IRecordPocoType _record;

    public AnonymousRecordWriter( ExportCodeWriterMap map, IRecordPocoType record )
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
                    .Append( "w.WriteStartArray();" ).NewLine();
        // The cells are positional: a cell whose type is excluded by the runtime filter keeps its position and
        // is written as null (the reader skips it), its value never leaves.
        foreach( var f in _record.Fields )
        {
            if( writers.NameMap.TypeSet.Contains( f.Type ) )
            {
                var cell = $"v.Item{f.Index + 1}";
                if( f.Type.IsPolymorphic )
                {
                    // The runtime type is filtered by WriteAny that writes nothing when it is excluded.
                    exporterType.Append( "if( !CK.Poco.Exc.JsonGen.Exporter.WriteAny( w, " ).Append( cell ).Append( ", wCtx ) ) w.WriteNullValue();" ).NewLine();
                }
                else
                {
                    exporterType.Append( "if( wCtx.RuntimeFilter.Contains( " ).Append( f.Type.Index >> 1 ).Append( " ) )" )
                                .OpenBlock();
                    writers.GetWriter( f.Type ).GenerateWrite( exporterType, f.Type, cell );
                    exporterType.CloseBlock()
                                .Append( "else w.WriteNullValue();" ).NewLine();
                }
            }
        }
        exporterType.Append( "w.WriteEndArray();" ).NewLine()
                    .CloseBlock();
    }
}
