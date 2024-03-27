using CK.CodeGen;
using CK.Core;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// IReadOnlyDictionary<TKey,TValue> is NOT covariant on TValue.
    /// When TValue is polymorphic we cannot use a IReadOnlyDictionary parameter
    /// in a simple "generic" helper.
    /// One solution could be to use both IEnumerable<TKey> Keys and IEnumerable<TValue> Values parameters
    /// but we prefer to handle only one parameter and we also want to avoid the projection of the values
    /// by the adapters.
    ///
    /// It is not easy to split the complexity here as we did for the list and set.
    /// We (unfortunately) generate a writer for each dictionary implementation type.
    ///
    /// The only good thing is that since we are bound to the exact type, we can optimize the write of
    /// nullable vs. non nullable reference type values.
    /// </summary>
    sealed class DictionaryWriter : JsonCodeWriter
    {
        readonly ICollectionPocoType _dictionary;

        public DictionaryWriter( ExportCodeWriterMap map, ICollectionPocoType dictionary )
            : base( map )
        {
            _dictionary = dictionary;
        }

        public override void RawWrite( ICodeWriter writer, string variableName )
        {
            writer.Append( "CK.Poco.Exc.JsonGen.Exporter.Write_" )
                  .Append( _dictionary.Index )
                  .Append( "( w, " ).Append( variableName ).Append( ", wCtx );" );
        }

        protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                     ICSCodeGenerationContext generationContext,
                                                     ExportCodeWriterMap writers,
                                                     ITypeScope exporterType,
                                                     ITypeScope pocoDirectoryType )
        {
            exporterType.Append( "internal static void Write_" ).Append( _dictionary.Index )
                        .Append( "(System.Text.Json.Utf8JsonWriter w, " )
                        .Append( _dictionary.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                        .OpenBlock();
            IPocoType tKey = _dictionary.ItemTypes[0];
            IPocoType tValue = _dictionary.ItemTypes[1];

            // String key: we write it as a JSON object.
            if( tKey.Type == typeof( string ) )
            {
                exporterType.Append( "w.WriteStartObject();" ).NewLine()
                            .Append( "foreach( var (k,e) in v )" )
                            .OpenBlock();
                if( tValue is IRecordPocoType )
                {
                    exporterType.Append( "w.WritePropertyName( k );" ).NewLine()
                                .Append( "var vLoc = e;" ).NewLine();
                    writers.GetWriter( tValue ).GenerateWrite( exporterType, tValue, "vLoc" );
                    exporterType.NewLine();
                }
                else if( tValue.IsPolymorphic )
                {
                    if( tValue.IsNullable )
                    {
                        exporterType.Append( """
                            if( e == null )
                            {
                                w.WritePropertyName( k );
                                w.WriteNullValue();
                            }
                            else
                            {
                            
                            """ );
                    }
                    exporterType.Append( """
                            int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( e.GetType(), -1 );
                            if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {e.GetType()}" );
                            if( !wCtx.RuntimeFilter.Contains( index >> 1 ) ) continue;
                            w.WritePropertyName( k );
                            CK.Poco.Exc.JsonGen.Exporter.WriteNonNullableFinalType( w, wCtx, index, e );
                            """ );
                    if( tValue.IsNullable ) exporterType.CloseBlock();
                }
                else
                {
                    exporterType.Append( "w.WritePropertyName( k );" ).NewLine();
                    writers.GetWriter( tValue ).GenerateWrite( exporterType, tValue, "e" );
                }
                exporterType.CloseBlock()
                    .Append( "w.WriteEndObject();" );
            }
            else
            {
                // A dictionary key cannot be polymorphic: this is the same as the JSON object above (the value may be polymorphic)
                // except that we write an array of arrays.
                Throw.DebugAssert( !tKey.IsPolymorphic );

                exporterType.Append( "w.WriteStartArray();" ).NewLine()
                            .Append( "foreach( var (k,e) in v )" )
                            .OpenBlock();

                if( tValue is IRecordPocoType )
                {
                    WriteStartEntry( exporterType, writers, tKey );
                    exporterType.NewLine().Append( "var vLoc = e;" ).NewLine();
                    writers.GetWriter( tValue ).GenerateWrite( exporterType, tValue, "vLoc" );
                    WriteEndEntry( exporterType );
                }
                else if( tValue.IsPolymorphic )
                {
                    if( tValue.IsNullable )
                    {
                        exporterType.Append( "if( e == null )" )
                                    .OpenBlock();
                        WriteStartEntry( exporterType, writers, tKey );
                        exporterType.Append( "w.WriteNullValue();" );
                        WriteEndEntry( exporterType );
                        exporterType.CloseBlock()
                                    .Append( "else" )
                                    .OpenBlock();
                    }
                    exporterType.Append( """
                                int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( e.GetType(), -1 );
                                if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {e.GetType()}" );
                                if( !wCtx.RuntimeFilter.Contains( index >> 1 ) ) continue;
                                """ );
                    WriteStartEntry( exporterType, writers, tKey );
                    exporterType.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteNonNullableFinalType( w, wCtx, index, e );" );
                    WriteEndEntry( exporterType );
                    if( tValue.IsNullable ) exporterType.CloseBlock();
                }
                else
                {
                    WriteStartEntry( exporterType, writers, tKey );
                    writers.GetWriter( tValue ).GenerateWrite( exporterType, tValue, "e" );
                    WriteEndEntry( exporterType );
                }
                exporterType.CloseBlock()
                    .Append( "w.WriteEndArray();" );
            }
            exporterType.CloseBlock();

            static void WriteStartEntry( ITypeScope code, ExportCodeWriterMap wMap, IPocoType tKey )
            {
                code.Append( "w.WriteStartArray();" ).NewLine();
                if( tKey is IRecordPocoType )
                {
                    code.Append( "var kLoc = k;" ).NewLine();
                    wMap.GetWriter( tKey ).GenerateWrite( code, tKey, "kLoc" );
                }
                else
                {
                    wMap.GetWriter( tKey ).GenerateWrite( code, tKey, "k" );
                }
            }

            static void WriteEndEntry( ITypeScope code )
            {
                code.NewLine().Append( "w.WriteEndArray();" ).NewLine();
            }
        }
    }

}
