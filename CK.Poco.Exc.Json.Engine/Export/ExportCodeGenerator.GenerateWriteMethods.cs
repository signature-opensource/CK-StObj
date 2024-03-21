//using CK.CodeGen;
//using CK.Core;
//using System.Linq;
//using System.Runtime.CompilerServices;
//using System.Runtime.Serialization;

//namespace CK.Setup.PocoJson
//{

//    sealed partial class ExportCodeGenerator
//    {
//        // Step 2: The actual Write methods are implemented only for the final types (that are not handled by a generic helper).
//        void GenerateWriteMethods( IActivityMonitor monitor )
//        {
//            foreach( var t in _nameMap.TypeSet.NonNullableTypes )
//            {
//                switch( t.Kind )
//                {
//                    case PocoTypeKind.PrimaryPoco:
//                        GeneratePocoWriteMethod( monitor, _generationContext, (IPrimaryPocoType)t );
//                        break;
//                    case PocoTypeKind.AnonymousRecord:
//                        var a = Unsafe.As<IAnonymousRecordPocoType>( t );
//                        if( a.IsUnnamed )
//                        {
//                            GenerateAnonymousRecordObliviousWriteMethod( _exporterType, a );
//                        }
//                        break;
//                    case PocoTypeKind.Record:
//                        GenerateNamedRecordWriteMethod( _exporterType, Unsafe.As<IRecordPocoType>( t ) );
//                        break;
//                    case PocoTypeKind.Array:
//                        ICollectionPocoType tA = (ICollectionPocoType)t;
//                        if( tA.ItemTypes[0].Type != typeof( byte ) )
//                        {
//                            GenerateArrayOrListWriteMethod( _exporterType, tA );
//                        }
//                        break;
//                    case PocoTypeKind.List:
//                        GenerateArrayOrListWriteMethod( _exporterType, (ICollectionPocoType)t );
//                        break;
//                    case PocoTypeKind.HashSet:
//                        GenerateHashSetWriteMethod( _exporterType, (ICollectionPocoType)t );
//                        break;
//                    case PocoTypeKind.Dictionary:
//                        GenerateDictionaryWriteMethod( _exporterType, (ICollectionPocoType)t );
//                        break;
//                }
//            }

//            void GenerateWriteJsonMethodHeader( ITypeScope code, IPocoType type )
//            {
//                code.Append( "internal static void Write_" )
//                    .Append( type.Index )
//                    .Append( "(System.Text.Json.Utf8JsonWriter w," );
//                if( type.Type.IsValueType ) code.Append( "ref " );
//                code.Append( type.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" );
//            }

//            void GenerateDictionaryWriteMethod( ITypeScope code, ICollectionPocoType type )
//            {
//                GenerateWriteJsonMethodHeader( code, type );
//                code.OpenBlock();
//                IPocoType tKey = type.ItemTypes[0];
//                IPocoType tValue = type.ItemTypes[1];
//                // String key: we write it as a JSON object.
//                if( tKey.Type == typeof( string ) )
//                {
//                    code.Append( "w.WriteStartObject();" ).NewLine()
//                        .Append( "foreach( var (k,e) in v )" )
//                        .OpenBlock();
//                    if( tValue is IRecordPocoType )
//                    {
//                        code.Append( "w.WritePropertyName( k );" ).NewLine()
//                            .Append( "var vLoc = e;" ).NewLine();
//                        _writerMap.GenerateWrite( code, tValue, "vLoc" );
//                        code.NewLine();
//                    }
//                    else if( tValue.IsPolymorphic )
//                    {
//                        code.Append( """
//                            if( e == null )
//                            {
//                                w.WritePropertyName( k );
//                                w.WriteNullValue();
//                            }
//                            else
//                            {
//                                int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( e.GetType(), -1 );
//                                if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {e.GetType().ToCSharpName(false)}" );
//                                if( !wCtx.RuntimeFilter.Contains( index >> 1 ) ) continue;
//                                w.WritePropertyName( k );
//                                CK.Poco.Exc.JsonGen.Exporter.WriteNonNullableFinalType( w, wCtx, index, e );
//                            }

//                            """ );
//                    }
//                    else
//                    {
//                        code.Append( "w.WritePropertyName( k );" ).NewLine();
//                        _writerMap.GenerateWrite( code, tValue, "e", trustNonNullableRef: true );
//                        code.NewLine();
//                    }
//                    code.CloseBlock()
//                        .Append( "w.WriteEndObject();" );
//                }
//                else
//                {
//                    // Key is not polymorphic: this is the same as the JSON object above (the value may be polymorphic).
//                    Throw.DebugAssert( !tKey.IsPolymorphic );

//                    code.Append( "w.WriteStartArray();" ).NewLine()
//                        .Append( "foreach( var (k,e) in v )" )
//                        .OpenBlock();

//                    if( tValue is IRecordPocoType )
//                    {
//                        WriteStartEntry( code, _writerMap, tKey );
//                        code.NewLine().Append( "var vLoc = e;" ).NewLine();
//                        _writerMap.GenerateWrite( code, tValue, "vLoc" );
//                        WriteEndEntry( code );
//                    }
//                    else if( tValue.IsPolymorphic )
//                    {
//                        code.Append( "if( e == null )" )
//                            .OpenBlock();
//                        WriteStartEntry( code, _writerMap, tKey );
//                        code.Append( "w.WriteNullValue();" );
//                        WriteEndEntry( code );
//                        code.CloseBlock()
//                            .Append( "else" )
//                            .OpenBlock()
//                            .Append( """
//                                int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( e.GetType(), -1 );
//                                if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {e.GetType().ToCSharpName(false)}" );
//                                if( !wCtx.RuntimeFilter.Contains( index >> 1 ) ) continue;

//                                """ );
//                        WriteStartEntry( code, _writerMap, tKey );
//                        code.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteNonNullableFinalType( w, wCtx, index, e );" );
//                        WriteEndEntry( code );
//                        code.CloseBlock();
//                    }
//                    else
//                    {
//                        WriteStartEntry( code, _writerMap, tKey );
//                        _writerMap.GenerateWrite( code, tValue, "e", trustNonNullableRef: true );
//                        WriteEndEntry( code );
//                    }
//                    code.CloseBlock()
//                        .Append( "w.WriteEndArray();" );
//                }
//                code.CloseBlock();

//                static void WriteStartEntry( ITypeScope code, WriterMap wMap, IPocoType tKey )
//                {
//                    code.Append( "w.WriteStartArray();" ).NewLine();
//                    if( tKey is IRecordPocoType )
//                    {
//                        code.Append( "var kLoc = k;" ).NewLine();
//                        wMap.GenerateWrite( code, tKey, "kLoc" );
//                    }
//                    else
//                    {
//                        wMap.GenerateWrite( code, tKey, "k", trustNonNullableRef: true );
//                    }
//                }

//                static void WriteEndEntry( ITypeScope code )
//                {
//                    code.NewLine().Append( "w.WriteEndArray();" ).NewLine();
//                }
//            }

//            void GenerateHashSetWriteMethod( ITypeScope code, ICollectionPocoType c )
//            {
//                var tI = c.ItemTypes[0];
//                // If the item is polymorphic or a Poco it is handled by generic helpers.
//                if( tI.IsPolymorphic || tI.Kind is PocoTypeKind.PrimaryPoco or PocoTypeKind.SecondaryPoco )
//                {
//                    return;
//                }
//                code.Append( "internal static void WriteS" ).Append( tI.Index )
//                    .Append( "(System.Text.Json.Utf8JsonWriter w," )
//                    .Append( c.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
//                    .OpenBlock()
//                    .Append( "w.WriteStartArray();" ).NewLine()
//                    .Append( "foreach( var item in v )" )
//                    .OpenBlock();

//                if( c.ItemTypes[0] is IRecordPocoType )
//                {
//                    code.Append( "var loc = item;" ).NewLine();
//                    _writerMap.GenerateWrite( code, tI, "loc" );
//                }
//                else
//                {
//                    _writerMap.GenerateWrite( code, tI, "item", trustNonNullableRef: true );
//                }
//                code.CloseBlock()
//                    .Append( "w.WriteEndArray();" ).NewLine()
//                    .CloseBlock();
//            }

//            void GenerateArrayOrListWriteMethod( ITypeScope code, ICollectionPocoType c )
//            {
//                var tI = c.ItemTypes[0];
//                // If the item is polymorphic or a Poco it is handled by generic helpers.
//                if( tI.IsPolymorphic || tI.Kind is PocoTypeKind.PrimaryPoco or PocoTypeKind.SecondaryPoco )
//                {
//                    return;
//                }
//                code.Append( "internal static void Write" ).Append( c.Kind == PocoTypeKind.Array ? "A" : "L" ).Append( tI.Index )
//                    .Append( "(System.Text.Json.Utf8JsonWriter w," )
//                    .Append( c.ImplTypeName ).Append( " v, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
//                    .OpenBlock()
//                    .Append( "w.WriteStartArray();" ).NewLine();
//                if( c.Kind == PocoTypeKind.Array )
//                {
//                    code.Append( "var a = v.AsSpan();" ).NewLine();
//                }
//                else
//                {
//                    code.Append( "var a = System.Runtime.InteropServices.CollectionsMarshal.AsSpan( v );" ).NewLine();
//                }
//                code.Append( "for( int i = 0; i < a.Length; ++i )" )
//                    .OpenBlock();
//                _writerMap.GenerateWrite( code, tI, "a[i]", trustNonNullableRef: true );
//                code.CloseBlock();
//                code.Append( "w.WriteEndArray();" ).NewLine();
//                code.CloseBlock();
//            }

//            void GenerateAnonymousRecordObliviousWriteMethod( ITypeScope code, IAnonymousRecordPocoType type )
//            {
//                GenerateWriteJsonMethodHeader( code, type );
//                code.OpenBlock()
//                    .Append( "w.WriteStartArray();" ).NewLine();
//                foreach( var f in type.Fields )
//                {
//                    if( _nameMap.TypeSet.Contains( f.Type ) )
//                    {
//                        _writerMap.GenerateWrite( code, f.Type, $"v.Item{f.Index+1}", trustNonNullableRef: true );
//                    }
//                }
//                code.Append( "w.WriteEndArray();" ).NewLine()
//                    .CloseBlock();
//            }

//            void GenerateNamedRecordWriteMethod( ITypeScope code, IRecordPocoType type )
//            {
//                Throw.DebugAssert( "Named records are their own oblivious.", type.IsOblivious );
//                GenerateWriteJsonMethodHeader( code, type );
//                code.OpenBlock()
//                              .Append( "w.WriteStartObject();" ).NewLine();
//                foreach( var f in type.Fields )
//                {
//                    if( _nameMap.TypeSet.Contains( f.Type ) )
//                    {
//                        GenerateWriteField( code, f, $"v.{f.Name}", requiresCopy: f.Type is IRecordPocoType );
//                    }
//                }
//                code.Append( "w.WriteEndObject();" ).NewLine()
//                              .CloseBlock();

//            }

//            void GeneratePocoWriteMethod( IActivityMonitor monitor, ICSCodeGenerationContext generationContext, IPrimaryPocoType type )
//            {
//                // Each Poco class is a PocoJsonExportSupport.IWriter.
//                var pocoClass = generationContext.GeneratedCode.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoClass );
//                pocoClass.Definition.BaseTypes.Add( new ExtendedTypeName( "PocoJsonExportSupport.IWriter" ) );

//                using( pocoClass.Region() )
//                {
//                    // The Write method.
//                    pocoClass.Append( "public bool WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx, bool withType )" )
//                             .OpenBlock()
//                             .Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( type.Index >> 1 ).Append( ") )")
//                             .OpenBlock()
//                             .Append( "if( withType )" )
//                             .OpenBlock()
//                             .Append( "w.WriteStartArray();" ).NewLine()
//                             .Append( "w.WriteStringValue(" ).AppendSourceString( _nameMap.GetName( type ) ).Append( ");" ).NewLine()
//                             .CloseBlock()
//                             .Append( "DoWriteJson( w, wCtx );" ).NewLine()
//                             .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
//                             .Append( "return true;" )
//                             .CloseBlock()
//                             .Append( "return false;" )
//                             .CloseBlock();

//                    pocoClass.Append( "public bool WriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
//                             .OpenBlock()
//                             .Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( type.Index >> 1 ).Append( ") )" )
//                             .OpenBlock()
//                             .Append( "DoWriteJson( w, wCtx );" ).NewLine()
//                             .Append( "return true;" ).NewLine()
//                             .CloseBlock()
//                             .Append( "return false;" ).NewLine()
//                             .CloseBlock();

//                    pocoClass.Append( "void DoWriteJson( System.Text.Json.Utf8JsonWriter w, CK.Poco.Exc.Json.PocoJsonWriteContext wCtx )" )
//                             .OpenBlock()
//                             .Append( "w.WriteStartObject();" ).NewLine()
//                             .Append( writer =>
//                             {
//                                 foreach( var f in type.Fields )
//                                 {
//                                     if( f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly
//                                         && _nameMap.TypeSet.Contains( f.Type ) )
//                                     {
//                                         Throw.DebugAssert( "If the field is a struct, it is by ref: we don't need a copy even for records.",
//                                                            f.Type is not IRecordPocoType r || f.FieldAccess == PocoFieldAccessKind.IsByRef );
//                                         GenerateWriteField( writer, f, f.PrivateFieldName, requiresCopy: false );
//                                     }
//                                 }
//                             } )
//                             .Append( "w.WriteEndObject();" ).NewLine()
//                             .CloseBlock();
//                }
//                var toString = FunctionDefinition.Parse( "public override string ToString()" );
//                if( pocoClass.FindFunction( toString.Key, false ) == null )
//                {
//                    using( pocoClass.Region() )
//                    {
//                        pocoClass
//                            .CreateFunction( toString )
//                            .Append( "var m = new System.Buffers.ArrayBufferWriter<byte>();" ).NewLine()
//                            .Append( "using( var w = new System.Text.Json.Utf8JsonWriter( m ) )" ).NewLine()
//                            .OpenBlock()
//                            .Append( "using var wCtx = new CK.Poco.Exc.Json.PocoJsonWriteContext( PocoDirectory_CK.Instance, CK.Poco.Exc.Json.PocoJsonExportOptions.ToStringDefault );" ).NewLine()
//                            .Append( "WriteJson( w, wCtx );" ).NewLine()
//                            .Append( "w.Flush();" ).NewLine()
//                            .CloseBlock()
//                            .Append( "return Encoding.UTF8.GetString( m.WrittenMemory.Span );" );
//                    }
//                }
//            }
//        }

//        void GenerateWriteField( ITypeScope code, IPocoField f, string implFieldName, bool requiresCopy )
//        {
//            Throw.DebugAssert( _nameMap.TypeSet.Contains( f.Type ) );
//            if( f.Type.IsPolymorphic )
//            {
//                if( f.Type.IsNullable )
//                {
//                    code.Append( "if( " ).Append( implFieldName ).Append( " == null )" )
//                    .OpenBlock();
//                    GenerateWritePropertyName( code, f.Name );
//                    code.Append( "w.WriteNullValue();" )
//                        .CloseBlock()
//                        .Append( "else" )
//                        .OpenBlock();
//                }
//                code.Append( "int index = PocoDirectory_CK.NonNullableFinalTypes.GetValueOrDefault( " ).Append( implFieldName ).Append( ".GetType(), -1 );" ).NewLine()
//                    .Append( """if( index < 0 ) w.ThrowJsonException( $"Non serializable type: {""" ).Append( implFieldName ).Append( """.GetType()}" );""" ).NewLine()
//                    .Append( "if( wCtx.RuntimeFilter.Contains( index >> 1 ) )" )
//                    .OpenBlock();
//                GenerateWritePropertyName( code, f.Name );
//                code.Append( "CK.Poco.Exc.JsonGen.Exporter.WriteNonNullableFinalType( w, wCtx, index, " ).Append( implFieldName ).Append( " );" )
//                    .CloseBlock();

//                if( f.Type.IsNullable ) code.CloseBlock();
//            }
//            else
//            {
//                code.Append( "if( wCtx.RuntimeFilter.Contains(" ).Append( f.Type.Index >> 1 ).Append( ") )" )
//                    .OpenBlock();
//                GenerateWritePropertyName( code, f.Name );
//                if( requiresCopy )
//                {
//                    var local = "loc" + f.Name;
//                    code.Append( "var " ).Append( local ).Append( " = " ).Append( implFieldName ).Append( ";" ).NewLine();
//                    implFieldName = local;
//                }
//                _writerMap.GenerateWrite( code, f.Type, implFieldName, trustNonNullableRef: true );
//                code.CloseBlock();
//            }
//        }

//        static void GenerateWritePropertyName( ICodeWriter writer, string name )
//        {
//            writer.Append( "w.WritePropertyName( wCtx.Options.UseCamelCase ? " )
//                  .AppendSourceString( System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName( name ) )
//                  .Append( " : " )
//                  .AppendSourceString( name )
//                  .Append( " );" ).NewLine();
//        }


//    }
//}
