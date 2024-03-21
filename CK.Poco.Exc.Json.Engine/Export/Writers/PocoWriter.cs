using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{


    sealed class PocoWriter : JsonCodeWriter
    {
        readonly IPrimaryPocoType _type;

        public PocoWriter( ExportCodeWriterMap map, IPrimaryPocoType type )
            : base( map )
        {
            _type = type;
        }

        public override void RawWrite( ICodeWriter writer, string variableName )
        {
            writer.Append( "((PocoJsonExportSupport.IWriter)" ).Append( variableName ).Append( ").WriteJson( w, wCtx );" );
        }

        protected override void GenerateSupportCode( IActivityMonitor monitor,
                                                     ICSCodeGenerationContext generationContext,
                                                     ExportCodeWriterMap writers,
                                                     ITypeScope exporterType,
                                                     ITypeScope pocoDirectoryType )
        {
            // Each Poco class is a PocoJsonExportSupport.IWriter.
            var pocoClass = generationContext.GeneratedCode.FindOrCreateAutoImplementedClass( monitor, _type.FamilyInfo.PocoClass );
            pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "PocoJsonExportSupport.IWriter" ) );

            using( pocoClass.Region() )
            {
                // The Write method.
                pocoClass.Append( "public bool WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx, bool withType )" )
                         .OpenBlock()
                         .Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( _type.Index >> 1 ).Append( ") )" )
                         .OpenBlock()
                         .Append( "if( withType )" )
                         .OpenBlock()
                         .Append( "w.WriteStartArray();" ).NewLine()
                         .Append( "w.WriteStringValue(" ).AppendSourceString( writers.NameMap.GetName( _type ) ).Append( ");" ).NewLine()
                         .CloseBlock()
                         .Append( "DoWriteJson( w, wCtx );" ).NewLine()
                         .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                         .Append( "return true;" )
                         .CloseBlock()
                         .Append( "return false;" )
                         .CloseBlock();

                pocoClass.Append( "public bool WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                         .OpenBlock()
                         .Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( _type.Index >> 1 ).Append( ") )" )
                         .OpenBlock()
                         .Append( "DoWriteJson( w, wCtx );" ).NewLine()
                         .Append( "return true;" ).NewLine()
                         .CloseBlock()
                         .Append( "return false;" ).NewLine()
                         .CloseBlock();

                pocoClass.Append( "void DoWriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                         .OpenBlock()
                         .Append( "w.WriteStartObject();" ).NewLine()
                         .Append( writer =>
                         {
                             foreach( var f in _type.Fields )
                             {
                                 if( f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly
                                     && writers.NameMap.TypeSet.Contains( f.Type ) )
                                 {
                                     Throw.DebugAssert( "If the field is a struct, it is by ref: we don't need a copy even for records.",
                                                        f.Type is not IRecordPocoType r || f.FieldAccess == PocoFieldAccessKind.IsByRef );
                                     GenerateWriteField( writer, writers, f, f.PrivateFieldName, requiresCopy: false );
                                 }
                             }
                         } )
                         .Append( "w.WriteEndObject();" ).NewLine()
                         .CloseBlock();
            }
            var toString = FunctionDefinition.Parse( "public override string ToString()" );
            if( pocoClass.FindFunction( toString.Key, false ) == null )
            {
                using( pocoClass.Region() )
                {
                    pocoClass
                        .CreateFunction( toString )
                        .Append( "var m = new System.Buffers.ArrayBufferWriter<byte>();" ).NewLine()
                        .Append( "using( var w = new System.Text.Json.Utf8JsonWriter( m ) )" ).NewLine()
                        .OpenBlock()
                        .Append( "using var wCtx = new CK.Poco.Exc.Json.PocoJsonWriteContext( PocoDirectory_CK.Instance, CK.Poco.Exc.Json.PocoJsonExportOptions.ToStringDefault );" ).NewLine()
                        .Append( "WriteJson( w, wCtx );" ).NewLine()
                        .Append( "w.Flush();" ).NewLine()
                        .CloseBlock()
                        .Append( "return Encoding.UTF8.GetString( m.WrittenMemory.Span );" );
                }
            }
        }

        internal static void GenerateWriteField( ITypeScope code, ExportCodeWriterMap writers, IPocoField f, string implFieldName, bool requiresCopy )
        {
            Throw.DebugAssert( writers.NameMap.TypeSet.Contains( f.Type ) );
            if( f.Type.IsPolymorphic )
            {
                if( f.Type.IsNullable )
                {
                    code.Append( "if( " ).Append( implFieldName ).Append( " == null )" )
                    .OpenBlock();
                    GenerateWritePropertyName( code, f.Name );
                    code.Append( "w.WriteNullValue();" )
                        .CloseBlock()
                        .Append( "else" )
                        .OpenBlock();
                }
                code.Append( "int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( " ).Append( implFieldName ).Append( ".GetType(), -1 );" ).NewLine()
                    .Append( """if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {""" ).Append( implFieldName ).Append( """.GetType()}" );""" ).NewLine()
                    .Append( "if( wCtx.RuntimeFilter.Contains( index >> 1 ) )" )
                    .OpenBlock();
                GenerateWritePropertyName( code, f.Name );
                code.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteNonNullableFinalType( w, wCtx, index, " ).Append( implFieldName ).Append( " );" )
                    .CloseBlock();

                if( f.Type.IsNullable ) code.CloseBlock();
            }
            else
            {
                code.Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( f.Type.Index >> 1 ).Append( ") )" )
                    .OpenBlock();
                GenerateWritePropertyName( code, f.Name );
                if( requiresCopy )
                {
                    var local = "loc" + f.Name;
                    code.Append( "var " ).Append( local ).Append( " = " ).Append( implFieldName ).Append( ";" ).NewLine();
                    implFieldName = local;
                }
                writers.GetWriter( f.Type ).GenerateWrite( code, f.Type, implFieldName );
                code.CloseBlock();
            }
        }

        static void GenerateWritePropertyName( ICodeWriter writer, string name )
        {
            writer.Append( "w.WritePropertyName( wCtx.Options.UseCamelCase ? " )
                  .AppendSourceString( System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( name ) )
                  .Append( " : " )
                  .AppendSourceString( name )
                  .Append( " );" ).NewLine();
        }

    }
}
