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
        JsonTypeInfo? TryRegisterInfoForValueTuple( NullableTypeTree t, IReadOnlyList<NullableTypeTree> types )
        {
            IJsonCodeGenHandler[] handlers = new IJsonCodeGenHandler[types.Count];
            bool isJSCanonical = true;
            var jsonName = new StringBuilder( "[" );
            var jsJsonName = new StringBuilder( "[" );
            for( int i = 0; i < types.Count; i++ )
            {
                if( i > 0 )
                {
                    jsonName.Append( ',' );
                    jsJsonName.Append( ',' );
                }
                var h = GetHandler( types[i] );
                if( h == null ) return null;
                handlers[i] = h;
                jsonName.Append( h.JsonName );
                isJSCanonical &= h.TypeInfo.ECMAScriptStandardJsonName.IsCanonical;
                jsJsonName.Append( h.TypeInfo.ECMAScriptStandardJsonName.Name );
            }
            jsonName.Append( ']' );
            jsJsonName.Append( ']' );
            JsonTypeInfo info = AllowTypeInfo( t.Type, jsonName.ToString() ).SetECMAScriptStandardName( jsJsonName.ToString(), isJSCanonical );

            var valueTupleName = t.ToCSharpName();
            // Don't use 'in' modifier on non-readonly structs: See https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/
            // We use a 'ref' instead (ValueTuple TypeInfo below use SetByRefWriter).
            var fWriteDef = FunctionDefinition.Parse( "internal static void WriteVT_" + info.NumberName + "( System.Text.Json.Utf8JsonWriter w, ref " + valueTupleName + " v, PocoJsonSerializerOptions options )" );
            var fReadDef = FunctionDefinition.Parse( "internal static void ReadVT_" + info.NumberName + "( ref System.Text.Json.Utf8JsonReader r, out " + valueTupleName + " v, PocoJsonSerializerOptions options )" );

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
                    fWrite.Append( "w.WriteStartArray();" ).NewLine();
                    int itemNumber = 0;
                    foreach( var h in handlers )
                    {
                        h.GenerateWrite( fWrite, "v.Item" + (++itemNumber).ToString( CultureInfo.InvariantCulture ) );
                    }
                    fWrite.Append( "w.WriteEndArray();" ).NewLine();

                    fRead.Append( "r.Read();" ).NewLine();

                    itemNumber = 0;
                    foreach( var h in handlers )
                    {
                        h.GenerateRead( fRead, "v.Item" + (++itemNumber).ToString( CultureInfo.InvariantCulture ), false );
                    }
                    fRead.Append( "r.Read();" ).NewLine();
                } );
            }

            info.SetByRefWriter()
                .Configure(
                  ( ICodeWriter write, string variableName ) =>
                  {
                      write.Append( "PocoDirectory_CK." ).Append( fWrite.Definition.MethodName.Name ).Append( "( w, ref " ).Append( variableName ).Append( ", options );" );
                  },
                  ( ICodeWriter read, string variableName, bool assignOnly, bool variableCanBeNull ) =>
                  {
                      string vName = variableName;
                      if( variableCanBeNull )
                      {
                          read.OpenBlock()
                              .AppendCSharpName( info.Type ).Append( " notNull;" ).NewLine();
                          vName = "notNull";
                      }
                      read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, out " ).Append( vName ).Append( ", options );" );
                      if( variableCanBeNull )
                      {
                          read.Append( variableName ).Append( " = notNull;" )
                              .CloseBlock();
                      }
                  } );

            return info;
        }
    }
}
