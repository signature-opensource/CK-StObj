using CK.CodeGen;
using CK.Core;
using System;
using System.Linq;

namespace CK.Setup.PocoJson
{
    class AnyWriter : JsonCodeWriter
    {
        public AnyWriter( ExportCodeWriterMap map )
            : base( map )
        {
        }

        public override void RawWrite( ICodeWriter writer, string variableName )
        {
            writer.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteAny( w, " ).Append( variableName ).Append( ", wCtx );" );
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
            exporterType.GeneratedByComment().Append( "static string[] _typeNames = new string?[] {" );
            // The array of names must contain all non nullable types.
            // Types that are not serializables have a null name.
            foreach( var type in writers.NameMap.TypeSystem.AllNonNullableTypes )
            {
                if( writers.NameMap.TypeSet.Contains( type ) )
                {
                    if( type.IsFinalType )
                    {
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
            }
            exporterType.Append( "};" ).NewLine();
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
                            w.WriteStringValue( _typeNames[index] );
                        }
                        switch( index )
                        {
                    """ );
            var types = writers.NameMap.TypeSet.NonNullableTypes.Where( t => t.IsFinalType );
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
                    internal static void WriteAny( System.Text.Json.Utf8JsonWriter w, object o, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )
                    {
                        int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( o.GetType(), -1 );
                        if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {o.GetType().ToCSharpName(false)}" );
                        if( wCtx.RuntimeFilter.Contains( index >> 1 ) )
                        {
                            WriteNonNullableFinalType( w, wCtx, index, o );
                        }
                    }
                    """ );

        }

    }
}
