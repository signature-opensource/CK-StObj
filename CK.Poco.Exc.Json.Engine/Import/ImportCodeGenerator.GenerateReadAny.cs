using CK.CodeGen;
using CK.Core;
using CK.Poco.Exc.Json.Import;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace CK.Setup.PocoJson
{
    sealed partial class ImportCodeGenerator
    {
        record struct Thing( string Name );

        static void InPlaceReader( ref Utf8JsonReader r, ref Thing v, PocoJsonImportOptions options )
        {
            // Type dependent code comes here.
        }

        static void InPlaceNullReader( ref Utf8JsonReader r, ref Thing? v, PocoJsonImportOptions options )
        {
            if( r.TokenType == JsonTokenType.Null )
            {
                r.Read();
                v = default;
            }
            else
            {
                InPlaceNullReader( ref r, ref v, options );
            }
        }

        void GenerateReadAny()
        {
            _importerType.GeneratedByComment()
                         .Append( @"
            delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.Export.PocoJsonImportOptions options );

            static readonly Dictionary<string, ReaderFunction> _typeReaders = new Dictionary<string, ReaderFunction>();

            static internal readonly object oFalse = false;
            static internal readonly object oTrue = true;

            internal static object? ReadAny( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )
            {
                object o;
                switch( r.TokenType )
                {
                    case System.Text.Json.JsonTokenType.Null:
                        o = null;
                        break;
                    case System.Text.Json.JsonTokenType.Number: o = r.GetDouble(); break;
                    case System.Text.Json.JsonTokenType.String: o = r.GetString(); break;
                    case System.Text.Json.JsonTokenType.False: o = oFalse; break;
                    case System.Text.Json.JsonTokenType.True: o = oTrue; break;
                    default:
                    {
                        if( r.TokenType != System.Text.Json.JsonTokenType.StartArray ) r.ThrowJsonException( ""Expected 2-cells array."" );
                        r.Read(); // [
                        var n = r.GetString();
                        r.Read();
                        if( !_typeReaders.TryGetValue( n, out var reader ) )
                        {
                            r.ThrowJsonException( $""Unregistered type name '{n}'."" );
                        }
                        if( r.TokenType == System.Text.Json.JsonTokenType.Null )
                        {
                            o = null;
                            r.Read();
                        }
                        else
                        {
                            o = reader( ref r, options );
                        }
                        if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) r.ThrowJsonException( ""Expected end of 2-cells array."" );
                        break;
                    }
                }
                r.Read(); 
                return o;
            }
" );

            // Configures the _typeReaders dictionary in the constructor.
            var ctor = _importerType.FindOrCreateFunction( "static Importer()" )
                                     .GeneratedByComment();
            //foreach( var t in _typeInfos )
            //{
            //    if( t == JsonTypeInfo.ObjectType ) continue;
            //    var h = t.NonNullHandler;
            //    Debug.Assert( h.JsonName != t.NullHandler.JsonName );
            //    Debug.Assert( t.GenCSharpName == h.GenCSharpName );
            //    ctor.OpenBlock()
            //        .Append( "// Type: " ).Append( t.Type.ToString() ).NewLine()
            //        .Append( "static object d( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options ) {" )
            //        .Append( h.GenCSharpName ).Append( " o;" ).NewLine();
            //    h.DoGenerateRead( ctor, "o", assignOnly: true, handleNull: false );
            //    ctor.NewLine().Append( "return o;" ).NewLine()
            //        .Append( "};" ).NewLine();
            //    ctor.Append( "_typeReaders.Add( " ).AppendSourceString( h.JsonName ).Append( ", d );" ).NewLine()
            //        .Append( "_typeReaders.Add( " ).AppendSourceString( t.NullHandler.JsonName ).Append( ", d );" ).NewLine()
            //    .CloseBlock();
            //}

            //foreach( var t in _standardReaders )
            //{
            //    var f = _pocoDirectory.Append( "static object ECMAScriptStandardRead_" ).Append( t.JsonName ).Append( "( ref System.Text.Json.Utf8JsonReader r, PocoJsonSerializerOptions options )" )
            //                          .OpenBlock();
            //    t.GenerateReadFunctionBody( f );
            //    _pocoDirectory.CloseBlock();

            //    ctor.Append( "_typeReaders.Add( " ).AppendSourceString( t.JsonName ).Append( ", ECMAScriptStandardRead_" ).Append( t.JsonName ).Append( " );" ).NewLine();
            //    if( t.MapNullableName )
            //    {
            //        ctor.Append( "_typeReaders.Add( " ).AppendSourceString( t.JsonName + '?' ).Append( ", ECMAScriptStandardRead_" ).Append( t.JsonName ).Append( " );" ).NewLine();
            //    }
            //}
        }

    }
}
