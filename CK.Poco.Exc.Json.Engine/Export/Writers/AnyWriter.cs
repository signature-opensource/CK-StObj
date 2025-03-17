using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson;

class AnyWriter : JsonCodeWriter
{
    public AnyWriter( ExportCodeWriterMap map )
        : base( map )
    {
    }

    public override void RawWrite( ICodeWriter writer, string variableName )
    {
        writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteAnyOrThrow( w, " ).Append( variableName ).Append( ", wCtx );" );
    }

    protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                 ICSCodeGenerationContext generationContext,
                                                 ExportCodeWriterMap writers,
                                                 ITypeScope exporterType,
                                                 ITypeScope pocoDirectoryType )
    {
        GenerateTypeNamesArray( exporterType, writers );
        GenerateWriteNonNullableFinalType( exporterType, writers );
        GenerateWriteAny( exporterType );
    }

    static void GenerateTypeNamesArray( ITypeScope exporterType, ExportCodeWriterMap writers )
    {
        exporterType.GeneratedByComment()
                    .Append( "static string[] _typeNames = new string?[] {" );
        // The array of names must contain all non nullable types.
        // Types that are not serializables have a null name.
        int userMessageTypeIndex = -1;
        int simpleUserMessageTypeIndex = -1;
        int index = 0;
        foreach( var type in writers.NameMap.TypeSystem.AllNonNullableTypes )
        {
            if( writers.NameMap.TypeSet.Contains( type ) )
            {
                if( type.IsFinalType )
                {
                    // Captures the index of UserMessage and SimpleUserMessage to be able
                    // to map UserMessage to SimpleUserMessage name when using polymorphism.
                    if( type.Type == typeof( UserMessage ) ) userMessageTypeIndex = index;
                    else if( type.Type == typeof( SimpleUserMessage ) ) simpleUserMessageTypeIndex = index;

                    exporterType.AppendSourceString( writers.NameMap.GetName( type ) );
                }
                else if( type.Nullable.IsFinalType )
                {
                    Throw.DebugAssert( "This is a reference type.", !type.Type.IsValueType );
                    exporterType.AppendSourceString( writers.NameMap.GetName( type ) );
                }
                else
                {
                    exporterType.Append( "null" );
                }
            }
            else
            {
                exporterType.Append( "null" );
            }
            exporterType.Append( "," );
            ++index;
        }
        exporterType.Append( "};" ).NewLine()
            .Append( "const int _userMessageTypeIndex = " ).Append( userMessageTypeIndex ).Append( ";" ).NewLine()
            .Append( "const int _simpleUserMessageTypeIndex = " ).Append( simpleUserMessageTypeIndex ).Append( ";" ).NewLine();
        // We need UserMessage => SimpleUserMessage.
        // This doesn't check that this is true for PocoTypeSet, this only check that the PocoTypeSystemBuilder
        // did the job correctly. A DebugAssert is enough here.
        Throw.DebugAssert( "PocoTypeSystemBuilder must have registered SimpleUserMessage when registering UserMessage.",
                            userMessageTypeIndex < 0 || simpleUserMessageTypeIndex != -1 );
    }

    static void GenerateWriteNonNullableFinalType( ITypeScope exporterType, ExportCodeWriterMap writers )
    {
        exporterType
            .GeneratedByComment()
            .Append( """
                internal static void WriteNonNullableFinalType( System.Text.Json.Utf8JsonWriter w,
                                                                CK.Poco.Exc.Json.PocoJsonWriteContext wCtx,
                                                                int index,
                                                                object o )
                {
                    index = index >> 1;
                    if( !wCtx.Options.TypeLess )
                    {
                        w.WriteStartArray();
                        int i = index == _userMessageTypeIndex && wCtx.Options.AlwaysExportSimpleUserMessage
                                            ? _simpleUserMessageTypeIndex
                                            : index;
                        w.WriteStringValue( _typeNames[i] );
                    }
                    switch( index )
                    {
                """ );
        var types = writers.NameMap.TypeSet.Where( t => t.IsFinalType );
        foreach( var t in types )
        {
            exporterType.Append( "case " ).Append( t.Index >> 1 ).Append( ":" )
                .OpenBlock();
            string variableName = $"(({t.ImplTypeName})o)";
            if( t is IRecordPocoType )
            {
                exporterType.Append( "var vLoc = " ).Append( variableName ).Append( ";" ).NewLine();
                variableName = "vLoc";
            }
            writers.GetWriter( t ).RawWrite( exporterType, variableName );
            exporterType.NewLine()
                .Append( "break;" )
                .CloseBlock();
        }
        exporterType.Append( """
                    }
                    if( !wCtx.Options.TypeLess ) w.WriteEndArray();
                }

                """ );
    }

    static void GenerateWriteAny( ITypeScope exporterType )
    {
        exporterType
            .GeneratedByComment()
            .Append( """
                // Used internally: type filtering must have already been done.
                internal static void WriteAnyOrThrow( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
                {
                    int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( o.GetType(), -1 );
                    if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {o.GetType().ToCSharpName(false)}" );
                    if( wCtx.RuntimeFilter.Contains( index >> 1 ) )
                    {
                        WriteNonNullableFinalType( w, wCtx, index, o );
                    }
                }

                internal static bool WriteAny( System.Text.Json.Utf8JsonWriter w, object? o, Poco.Exc.Json.PocoJsonWriteContext wCtx )  
                {
                    if( o == null )
                    {
                        w.WriteNullValue();
                        return true;
                    }
                    int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( o.GetType(), -1 );
                    if( index >= 0 && wCtx.RuntimeFilter.Contains( index >> 1 ) )
                    {
                        WriteNonNullableFinalType( w, wCtx, index, o );
                        return true;
                    }
                    return false;
                }
                
                """ );

    }

}
