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
                      ( ICodeWriter read, string variableName, bool assignOnly, bool isNullableVariable ) =>
                      {
                          if( !assignOnly )
                          {
                              if( isNullableVariable )
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
                          // We cast the variable into its type to handle the case where the variable is an 'object' (or
                          // any other base type).
                          read.Append( "PocoDirectory_CK." )
                              .Append( fRead.Definition.MethodName.Name )
                              .Append( "( ref r, (" )
                              .AppendCSharpName( info.Type )
                              .Append( ")" )
                              .Append( variableName )
                              .Append( ", options );" );
                      } );
            AllowTypeInfo( info );
            // The interface maps to the collection type.
            AllowTypeAlias( tInterface, info );
            return info.NullHandler;
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateMapFunctions( NullableTypeTree tMap, NullableTypeTree tKey, NullableTypeTree tValue )
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
                    
                    fRead.Append( keyTypeName ).Append( " k;" ).NewLine();
                    keyHandler.GenerateRead( fRead, "k", true );

                    fRead.NewLine()
                         .Append( valueTypeName ).Append( " v;" ).NewLine();
                    valueHandler.GenerateRead( fRead, "v", true );

                    fRead.Append( "r.Read();" ).NewLine()
                         .Append( "c.Add( k, v );" )
                         .CloseBlock()
                         .Append( "r.Read();" );
                } );
            }
            var info = CreateTypeInfo( tMap.Type, "M(" + keyHandler.JsonName + "," + valueHandler.JsonName + ")" )
                       .SetECMAScriptStandardName( name: "M(" + keyHandler.TypeInfo.ECMAScriptStandardJsonName.Name + "," + valueHandler.TypeInfo.ECMAScriptStandardJsonName.Name + ")",
                                                   isCanonical: keyHandler.TypeInfo.ECMAScriptStandardJsonName.IsCanonical && valueHandler.TypeInfo.ECMAScriptStandardJsonName.IsCanonical );
            return (fWrite, fRead, info);
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateStringMapFunctions( NullableTypeTree tMap, NullableTypeTree tValue )
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
            var info = CreateTypeInfo( tMap, "O(" + valueHandler.JsonName + ")" )
                      .SetECMAScriptStandardName( "O(" + valueHandler.TypeInfo.ECMAScriptStandardJsonName.Name + ")", valueHandler.TypeInfo.ECMAScriptStandardJsonName.IsCanonical );
            return (fWrite, fRead, info);
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateListOrSetFunctions( NullableTypeTree tColl, bool isList )
        {
            NullableTypeTree tItem = tColl.RawSubTypes[0];

            if( !CreateWriteEnumerable( tItem, out IFunctionScope? fWrite, out IJsonCodeGenHandler? itemHandler ) ) return default;

            var fReadDef = FunctionDefinition.Parse( "internal static void ReadLOrS_" + itemHandler.NumberName + "( ref System.Text.Json.Utf8JsonReader r, ICollection<" + itemHandler.Type.ToCSharpName() + "> c, PocoJsonSerializerOptions options )" );
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
            var info = CreateTypeInfo( tColl, (isList ? "L(" : "S(") + itemHandler.JsonName + ")" )
                       .SetECMAScriptStandardName( name: isList ? itemHandler.TypeInfo.ECMAScriptStandardJsonName.Name + "[]" : "S(" + itemHandler.TypeInfo.ECMAScriptStandardJsonName.Name + ")",
                                                   isCanonical: isList && itemHandler.TypeInfo.ECMAScriptStandardJsonName.IsCanonical );

            return (fWrite, fRead, info);
        }

        (IFunctionScope fWrite, IFunctionScope fRead, JsonTypeInfo info) CreateArrayFunctions( Type tArray, Type tItem )
        {
            Debug.Assert( tArray.IsArray );

            // We don't really need to call CreateWriteEnumerable here: List<TItem> has already done it but
            // this call gives us the fWrite and the itemHandler.
            // Note: Keeping the if here is useless but this ensures the non nullability of the out parameters.
            if( !CreateWriteEnumerable( tItem, out IFunctionScope? fWrite, out IJsonCodeGenHandler? itemHandler ) ) return default;

            var fReadDef = FunctionDefinition.Parse( "internal static void ReadArray_" + itemHandler.NumberName + "( ref System.Text.Json.Utf8JsonReader r, out " + itemHandler.Type.ToCSharpName() + "[] a, PocoJsonSerializerOptions options )" );
            IFunctionScope? fRead = _pocoDirectory.FindFunction( fReadDef.Key, false );
            if( fRead == null )
            {
                fRead = _pocoDirectory.CreateFunction( fReadDef );
                fRead.OpenBlock()
                     .Append( "var c = new List<" + itemHandler.Type.ToCSharpName() + ">();" ).NewLine()
                     .Append( "ReadLOrS_" + itemHandler.NumberName + "( ref r, c, options );" ).NewLine()
                     .Append( "a = c.ToArray();" ).NewLine()
                     .CloseBlock();
            }
            var info = CreateTypeInfo( tArray, itemHandler.JsonName + "[]" )
                       .SetECMAScriptStandardName( itemHandler.TypeInfo.ECMAScriptStandardJsonName.Name + "[]", false );
            return (fWrite, fRead, info);
        }

        bool CreateWriteEnumerable( NullableTypeTree tItem, [NotNullWhen( true )] out IFunctionScope? fWrite, [NotNullWhen( true )] out IJsonCodeGenHandler? itemHandler )
        {
            fWrite = null;
            itemHandler = GetHandler( tItem );
            if( itemHandler != null )
            {
                var fWriteDef = FunctionDefinition.Parse( "internal static void WriteE_" + itemHandler.NumberName + "( System.Text.Json.Utf8JsonWriter w, IEnumerable<" + itemHandler.Type.ToCSharpName() + "> c, PocoJsonSerializerOptions options )" );
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
                        // 'ref' cannot be used on foreach variable. We need a copy.
                        if( closeItemHandler.TypeInfo.ByRefWriter )
                        {
                            closeFWrite.Append( "var nR = e;" );
                            closeItemHandler.GenerateWrite( closeFWrite, "nR" );
                        }
                        else
                        {
                            closeItemHandler.GenerateWrite( closeFWrite, "e" );
                        }
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
