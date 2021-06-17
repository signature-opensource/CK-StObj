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
        JsonTypeInfo? TryRegisterInfoForValueTuple( Type t, Type[] types )
        {
            IJsonCodeGenHandler[] handlers = new IJsonCodeGenHandler[types.Length];
            var b = new StringBuilder( "[" );
            for( int i = 0; i < types.Length; i++ )
            {
                if( i > 0 ) b.Append( ',' );
                var h = GetHandler( types[i] );
                if( h == null ) return null;
                handlers[i] = h;
                b.Append( h.Name );
            }
            b.Append( ']' );
            JsonTypeInfo info = AllowTypeInfo( t, b.ToString(), StartTokenType.Array );

            var valueTupleName = t.ToCSharpName();
            // Don't use 'in' modifier on non-readonly structs: See https://devblogs.microsoft.com/premier-developer/the-in-modifier-and-the-readonly-structs-in-c/
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
                  ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                  {
                      string vName = variableName;
                      if( isNullable )
                      {
                          read.OpenBlock()
                              .AppendCSharpName( info.Type ).Space().Append( "notNull;" ).NewLine();
                          vName = "notNull";
                      }
                      read.Append( "PocoDirectory_CK." ).Append( fRead.Definition.MethodName.Name ).Append( "( ref r, out " ).Append( vName ).Append( ", options );" );
                      if( isNullable )
                      {
                          read.Append( variableName ).Append( " = notNull;" )
                              .CloseBlock();
                      }
                  } );

            return info;
        }
    }
}
