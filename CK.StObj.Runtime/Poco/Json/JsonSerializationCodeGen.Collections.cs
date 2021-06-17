using CK.CodeGen;
using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CK.Setup.Json
{

    public partial class JsonSerializationCodeGen
    {
        IJsonCodeGenHandler ConfigureAndAddTypeInfoForListSetAndMap( JsonTypeInfo info, IFunctionScope fWrite, IFunctionScope fRead, Type tInterface )
        {
            Debug.Assert( !info.Type.IsInterface && tInterface.IsInterface );
            info.Configure(
                      ( ICodeWriter write, string variableName ) =>
                      {
                          write.Append( "PocoDirectory_CK." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, " ).Append( variableName ).Append( ", options );" );
                      },
                      ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                      {
                          if( !assignOnly )
                          {
                              if( isNullable )
                              {
                                  read.Append( "if( " ).Append( variableName ).Append( " == null )" )
                                      .OpenBlock()
                                      .Append( variableName ).Append( " = new " ).AppendCSharpName( info.Type ).Append( "();" )
                                      .CloseBlock();
                              }
                              else
                              {
                                  read.Append( variableName ).Append( ".Clear();" ).NewLine();
                              }
                          }
                          else
                          {
                              read.Append( variableName ).Append( " = new " ).AppendCSharpName( info.Type ).Append( "();" ).NewLine();
                          }
                          read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, " ).Append( variableName ).Append( ", options );" );
                      } );
            AllowTypeInfo( info );
            // The interface is directly mapped to the non null handler.
            AddTypeHandlerAlias( tInterface, info.NonNullHandler );
            return info.NullHandler;
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateMapFunctions( Type tMap, Type tKey, Type tValue )
        {
            var keyHandler = GetHandler( tKey );
            if( keyHandler == null ) return default;
            var valueHandler = GetHandler( tValue );
            if( valueHandler == null ) return default;

            string keyTypeName = keyHandler.Type.ToCSharpName();
            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<" + keyTypeName + "," + valueTypeName + ">";

            string funcSuffix = keyHandler.TypeInfo.NumberName + "_" + valueHandler.TypeInfo.NumberName;
            // Trick: the reader/writer functions accepts the interface rather than the concrete type.
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteM_" + funcSuffix + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c, PocoJsonSerializerOptions options )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadM_" + funcSuffix + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c, PocoJsonSerializerOptions options )" );
            IFunctionScope? fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = _pocoDirectory.CreateFunction( fWriteDef );
                fRead = _pocoDirectory.CreateFunction( fReadDef );

                _finalReadWrite.Add( m =>
                {
                    fWrite.Append( "w.WriteStartArray();" ).NewLine()
                          .Append( "foreach( var e in c )" )
                          .OpenBlock()
                          .Append( "w.WriteStartArray();" ).NewLine();

                    keyHandler.GenerateWrite( fWrite, "e.Key" );
                    valueHandler.GenerateWrite( fWrite, "e.Value" );

                    fWrite.Append( "w.WriteEndArray();" )
                          .CloseBlock()
                          .Append( "w.WriteEndArray();" ).NewLine();

                    fRead.Append( "r.Read();" ).NewLine()
                         .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray)" )
                         .OpenBlock()
                         .Append( "r.Read();" ).NewLine();

                    fRead.AppendCSharpName( tKey ).Append( " k;" ).NewLine();
                    keyHandler.GenerateRead( fRead, "k", true );

                    fRead.NewLine()
                         .AppendCSharpName( tValue ).Append( " v;" ).NewLine();
                    valueHandler.GenerateRead( fRead, "v", true );

                    fRead.Append( "r.Read();" ).NewLine()
                         .Append( "c.Add( k, v );" )
                         .CloseBlock()
                         .Append( "r.Read();" );
                } );
            }
            var info = CreateTypeInfo( tMap, "M(" + keyHandler.Name + "," + valueHandler.Name + ")", StartTokenType.Array )
                       .SetECMAScriptStandardName( "M(" + keyHandler.ECMAScriptStandardName + "," + valueHandler.ECMAScriptStandardName + ")" );
            return (fWrite, fRead, info);
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateStringMapFunctions( Type tMap, Type tValue )
        {
            var valueHandler = GetHandler( tValue );
            if( valueHandler == null ) return default;

            string valueTypeName = valueHandler.Type.ToCSharpName();
            var concreteTypeName = "Dictionary<string," + valueTypeName + ">";
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteO_" + valueHandler.TypeInfo.NumberName + "( System.Text.Json.Utf8JsonWriter w, I" + concreteTypeName + " c, PocoJsonSerializerOptions options )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadO_" + valueHandler.TypeInfo.NumberName + "( ref System.Text.Json.Utf8JsonReader r, I" + concreteTypeName + " c, PocoJsonSerializerOptions options )" );
            IFunctionScope? fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
            IFunctionScope? fRead;
            if( fWrite != null )
            {
                fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
                Debug.Assert( fRead != null );
            }
            else
            {
                fWrite = _pocoDirectory.CreateFunction( fWriteDef );
                fRead = _pocoDirectory.CreateFunction( fReadDef );

                _finalReadWrite.Add( m =>
                {
                    fWrite.Append( "w.WriteStartObject();" ).NewLine()
                          .Append( "foreach( var e in c )" )
                          .OpenBlock()
                          .Append( "w.WritePropertyName( e.Key );" );
                    valueHandler.GenerateWrite( fWrite, "e.Value" );
                    fWrite.CloseBlock()
                     .Append( "w.WriteEndObject();" ).NewLine();

                    fRead.Append( "r.Read();" ).NewLine()
                        .AppendCSharpName( tValue ).Append( " v;" ).NewLine()
                        .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndObject )" )
                        .OpenBlock()
                        .Append( "string k = r.GetString();" ).NewLine()
                        .Append( "r.Read();" ).NewLine();
                    valueHandler.GenerateRead( fRead, "v", false );
                    fRead.Append( "c.Add( k, v );" )
                         .CloseBlock()
                         .Append( "r.Read();" );
                } );
            }
            var info = CreateTypeInfo( tMap, "O(" + valueHandler.Name + ")", StartTokenType.Object )
                      .SetECMAScriptStandardName( "O(" + valueHandler.ECMAScriptStandardName + ")" );
            return (fWrite, fRead, info);
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateListOrSetFunctions( Type tColl, bool isList )
        {
            Type tItem = tColl.GetGenericArguments()[0];

            if( !CreateWriteEnumerable( tItem, out IFunctionScope? fWrite, out IJsonCodeGenHandler? itemHandler, out string? itemTypeName ) ) return default;

            var fReadDef = FunctionDefinition.Parse( "internal static void ReadLOrS_" + itemHandler.TypeInfo.NumberName + "( ref System.Text.Json.Utf8JsonReader r, ICollection<" + itemTypeName + "> c, PocoJsonSerializerOptions options )" );
            IFunctionScope? fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
            if( fRead == null )
            {
                fRead = _pocoDirectory.CreateFunction( fReadDef );
                _finalReadWrite.Add( m =>
                {
                    fRead.Append( "r.Read();" ).NewLine()
                         .AppendCSharpName( tItem ).Append( " v;" ).NewLine()
                         .Append( "while( r.TokenType != System.Text.Json.JsonTokenType.EndArray )" )
                         .OpenBlock();
                    itemHandler.GenerateRead( fRead, "v", false );
                    fRead.Append( "c.Add( v );" )
                         .CloseBlock()
                         .Append( "r.Read();" );
                } );
            }
            var info = CreateTypeInfo( tColl, (isList ? "L(" : "S(") + itemHandler.Name + ")", StartTokenType.Array )
                       .SetECMAScriptStandardName( isList ? itemHandler.ECMAScriptStandardName + "[]" : "S(" + itemHandler.ECMAScriptStandardName + ")" );

            return (fWrite, fRead, info);
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateArrayFunctions( Type tArray )
        {
            Debug.Assert( tArray.IsArray );
            Type tItem = tArray.GetElementType()!;

            if( !CreateWriteEnumerable( tItem, out IFunctionScope? fWrite, out IJsonCodeGenHandler? itemHandler, out string? itemTypeName ) ) return default;

            var fReadDef = FunctionDefinition.Parse( "internal static void ReadArray_" + itemHandler.TypeInfo.NumberName + "( ref System.Text.Json.Utf8JsonReader r, out " + itemTypeName + "[] a, PocoJsonSerializerOptions options )" );
            IFunctionScope? fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
            if( fRead == null )
            {
                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.OpenBlock()
                     .Append( "var c = new List<" + itemTypeName + ">();" ).NewLine()
                     .Append( "ReadLOrS_" + itemHandler.TypeInfo.NumberName + "( ref r, c, options );" ).NewLine()
                     .Append( "a = c.ToArray();" ).NewLine()
                     .CloseBlock();
            }
            var info = CreateTypeInfo( tArray, itemHandler.Name + "[]", StartTokenType.Array )
                       .SetECMAScriptStandardName( itemHandler.ECMAScriptStandardName + "[]" );
            return (fWrite, fRead, info);
        }

        bool CreateWriteEnumerable( Type tItem,
                                    [NotNullWhen( true )] out IFunctionScope? fWrite,
                                    [NotNullWhen( true )] out IJsonCodeGenHandler? itemHandler,
                                    [NotNullWhen( true )] out string? itemTypeName )
        {
            fWrite = null;
            itemTypeName = null;
            itemHandler = GetHandler( tItem );
            if( itemHandler != null )
            {
                itemTypeName = itemHandler.Type.ToCSharpName();
                var fWriteDef = FunctionDefinition.Parse( "internal static void WriteE_" + itemHandler.TypeInfo.NumberName + "( System.Text.Json.Utf8JsonWriter w, IEnumerable<" + itemTypeName + "> c, PocoJsonSerializerOptions options )" );
                fWrite = _pocoDirectory.FindFunction( fWriteDef.Key, false );
                if( fWrite == null )
                {
                    fWrite = _pocoDirectory.CreateFunction( fWriteDef );

                    var closeItemHandler = itemHandler;
                    var closeFWrite = fWrite;
                    _finalReadWrite.Add( m =>
                    {
                        closeFWrite.Append( "w.WriteStartArray();" ).NewLine()
                                   .Append( "foreach( var e in c )" )
                                   .OpenBlock();
                        closeItemHandler.GenerateWrite( closeFWrite, "e" );
                        closeFWrite.CloseBlock()
                                   .Append( "w.WriteEndArray();" ).NewLine();
                    } );
                }
                return true;
            }
            return false;
        }

    }
}
