using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{

    sealed partial class ExportCodeGenerator
    {
        // Step 2: The actual Write methods are implemented only for the Exchangeable, NonNullable, and Oblivious types.
        void GenerateWriteMethods( IActivityMonitor monitor )
        {
            foreach( var type in _nameMap.TypeSet.NonNullableTypes.Where( t => t.IsOblivious ) )
            {
                switch( type.Kind )
                {
                    case PocoTypeKind.PrimaryPoco:
                        GeneratePocoWriteMethod( monitor, _generationContext, (IPrimaryPocoType)type );
                        break;
                    case PocoTypeKind.AnonymousRecord:
                        GenerateAnonymousRecordWriteMethod( _exporterType, (IRecordPocoType)type );
                        break;
                    case PocoTypeKind.Record:
                        GenerateNamedRecordWriteMethod( _exporterType, (IRecordPocoType)type );
                        break;
                    case PocoTypeKind.Array:
                        ICollectionPocoType tA = (ICollectionPocoType)type;
                        if( tA.ItemTypes[0].Type != typeof( byte ) )
                        {
                            GenerateListOrArrayWriteMethod( _exporterType, tA );
                        }
                        break;
                    case PocoTypeKind.List:
                        GenerateListOrArrayWriteMethod( _exporterType, (ICollectionPocoType)type );
                        break;
                    case PocoTypeKind.HashSet:
                        GenerateHashSetWriteMethod( _exporterType, (ICollectionPocoType)type );
                        break;
                    case PocoTypeKind.Dictionary:
                        GenerateDictionaryWriteMethod( _exporterType, (ICollectionPocoType)type );
                        break;
                }
            }

            void GenerateWriteJsonMethodHeader( ITypeScope code, IPocoType type )
            {
                code.Append( "internal static void Write_" )
                    .Append( type.Index )
                    .Append( "(System.Text.Json.Utf8JsonWriter w," );
                if( type.Type.IsValueType ) code.Append( "ref " );
                code.Append( type.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" );
            }

            void GenerateDictionaryWriteMethod( ITypeScope code, ICollectionPocoType type )
            {
                GenerateWriteJsonMethodHeader( code, type );
                code.OpenBlock();
                if( type.ItemTypes[0].Type == typeof( string ) )
                {
                    code.Append( "w.WriteStartObject();" ).NewLine()
                                    .Append( "foreach( var item in v )" )
                                    .OpenBlock()
                                    .Append( "w.WritePropertyName( item.Key );" ).NewLine()
                                    .Append( "var vLoc = item.Value;" ).NewLine()
                                    .Append( writer => GenerateWrite( writer, type.ItemTypes[1], "vLoc" ) ).NewLine()
                                    .CloseBlock()
                                    .Append( "w.WriteEndObject();" );
                }
                else
                {
                    code.Append( "w.WriteStartArray();" ).NewLine()
                                    .Append( "foreach( var (k,e) in v )" )
                                    .OpenBlock()
                                    .Append( "w.WriteStartArray();" ).NewLine()
                                    .Append( "var tK = k;" ).NewLine()
                                    .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "tK" ) ).NewLine()
                                    .Append( "var tE = e;" ).NewLine()
                                    .Append( writer => GenerateWrite( writer, type.ItemTypes[1], "tE" ) ).NewLine()
                                    .Append( "w.WriteEndArray();" ).NewLine()
                                    .CloseBlock()
                                    .Append( "w.WriteEndArray();" );
                }
                code.CloseBlock();
            }

            void GenerateHashSetWriteMethod( ITypeScope code, ICollectionPocoType type )
            {
                GenerateWriteJsonMethodHeader( code, type );
                code.OpenBlock();
                code.Append( "w.WriteStartArray();" ).NewLine()
                    .Append( "foreach( var item in v )" )
                    .OpenBlock();

                if( type.ItemTypes[0].Type.IsValueType )
                {
                    code.Append( "var loc = item;" ).NewLine()
                        .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "loc" ) );
                }
                else
                {
                    code.Append( writer => GenerateWrite( writer, type.ItemTypes[0], "item" ) );
                }
                code.CloseBlock()
                    .Append( "w.WriteEndArray();" ).NewLine()
                    .CloseBlock();
            }

            void GenerateListOrArrayWriteMethod( ITypeScope code, ICollectionPocoType type )
            {
                GenerateWriteJsonMethodHeader( code, type );
                code.OpenBlock();
                code.Append( "w.WriteStartArray();" ).NewLine();
                if( type.ItemTypes[0].Type.IsValueType )
                {
                    if( type.Kind == PocoTypeKind.Array )
                    {
                        code.Append( "var a = v.AsSpan();" ).NewLine();
                    }
                    else
                    {
                        code.Append( "var a = System.Runtime.InteropServices.CollectionsMarshal.AsSpan( v );" ).NewLine();
                    }
                    code.Append( "for( int i = 0; i < a.Length; ++i )" )
                        .OpenBlock()
                        .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "a[i]" ) )
                        .CloseBlock();
                }
                else
                {
                    code.Append( "foreach( var e in v )" )
                        .OpenBlock()
                        .Append( writer => GenerateWrite( writer, type.ItemTypes[0], "e" ) )
                        .CloseBlock();
                }
                code.Append( "w.WriteEndArray();" ).NewLine();
                code.CloseBlock();
            }

            void GenerateAnonymousRecordWriteMethod( ITypeScope code, IRecordPocoType type )
            {
                GenerateWriteJsonMethodHeader( code, type );
                code.OpenBlock()
                    .Append( "w.WriteStartArray();" ).NewLine();
                foreach( var f in type.Fields )
                {
                    if( _nameMap.TypeSet.Contains( f.Type ) )
                    {
                        GenerateWrite( code, f.Type, $"v.Item{f.Index+1}" );
                    }
                }
                code.Append( "w.WriteEndArray();" ).NewLine()
                    .CloseBlock();
            }

            void GenerateNamedRecordWriteMethod( ITypeScope writer, IRecordPocoType type )
            {
                GenerateWriteJsonMethodHeader( writer, type );
                writer.OpenBlock()
                              .Append( "w.WriteStartObject();" ).NewLine();
                foreach( var f in type.Fields )
                {
                    if( _nameMap.TypeSet.Contains( f.Type ) )
                    {
                        GenerateWriteField( writer, f, $"v.{f.Name}", requiresCopy: f.Type is IRecordPocoType );
                    }
                }
                writer.Append( "w.WriteEndObject();" ).NewLine()
                              .CloseBlock();

            }

            void GeneratePocoWriteMethod( IActivityMonitor monitor, ICSCodeGenerationContext generationContext, IPrimaryPocoType type )
            {
                // Each Poco class is a PocoJsonExportSupport.IWriter.
                var pocoClass = generationContext.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoClass );
                pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "PocoJsonExportSupport.IWriter" ) );

                using( pocoClass.Region() )
                {
                    // The Write method.
                    pocoClass.Append( "public bool WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx, bool withType )" )
                             .OpenBlock()
                             .Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( type.Index >> 1 ).Append( ") )")
                             .OpenBlock()
                             .Append( "if( withType )" )
                             .OpenBlock()
                             .Append( "w.WriteStartArray();" ).NewLine()
                             .Append( "w.WriteStringValue(" ).AppendSourceString( _nameMap.GetName( type ) ).Append( ");" ).NewLine()
                             .CloseBlock()
                             .Append( "DoWriteJson( w, wCtx );" ).NewLine()
                             .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                             .Append( "return true;" )
                             .CloseBlock()
                             .Append( "return false;" )
                             .CloseBlock();

                    pocoClass.Append( "public bool WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
                             .OpenBlock()
                             .Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( type.Index >> 1 ).Append( ") )" )
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
                                 foreach( var f in type.Fields )
                                 {
                                     if( f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly
                                         && _nameMap.TypeSet.Contains( f.Type ) )
                                     {
                                         Throw.DebugAssert( "If the field is a struct, it is by ref.",
                                                            f.Type is not IRecordPocoType r || f.FieldAccess == PocoFieldAccessKind.IsByRef );
                                         GenerateWriteField( writer, f, f.PrivateFieldName, requiresCopy: false );
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
        }

        void GenerateWriteField( ITypeScope writer, IPocoField f, string implFieldName, bool requiresCopy )
        {
            Throw.DebugAssert( _nameMap.TypeSet.Contains( f.Type ) );
            writer.Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( f.Type.Index >> 1 ).Append( ") )" )
                  .OpenBlock();
            GenerateWritePropertyName( writer, f.Name );
            if( requiresCopy )
            {
                var local = "loc" + f.Name;
                writer.Append( "var " ).Append( local ).Append( " = " ).Append( implFieldName ).Append( ";" ).NewLine();
                implFieldName = local;
            }
            GenerateWrite( writer, f.Type, implFieldName );
            writer.CloseBlock();
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
