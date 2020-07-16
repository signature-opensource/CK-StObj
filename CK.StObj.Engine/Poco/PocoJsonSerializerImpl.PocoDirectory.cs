using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

#nullable enable

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r );

        void FillDynamicMaps( Dictionary<object, IHandler> map )
        {
            PocoDirectory.Append( @"
            delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r );
            delegate void WriterFunction( System.Text.Json.Utf8JsonWriter w, object o );

            Dictionary<string, ReaderFunction> _typeReaders = new Dictionary<string, ReaderFunction>();
            Dictionary<Type, WriterFunction> _typeWriters = new Dictionary<Type, WriterFunction>();" ).NewLine();

            var ctor = PocoDirectory.FindOrCreateFunction( "public PocoDirectory_CK()" );
            foreach( var (type,handler) in map )
            {
                if( handler.IsNullable
                    || (handler.Info.DirectType != DirectType.None && handler.Info.DirectType != DirectType.Untyped) ) continue;
                if( type is string name )
                {
                    if( name.Length == 0 ) continue;
                    ctor.Append( "_typeReaders.Add( " )
                        .AppendSourceString( name )
                        .Append( ", delegate ( ref System.Text.Json.Utf8JsonReader r )" )
                        .OpenBlock()
                        .AppendCSharpName( handler.Type ).Append( " o;" ).NewLine();
                    handler.GenerateRead( ctor, "o", true, FromPocoDirectory );
                    ctor.NewLine().Append( "return o;" )
                        .CloseBlock()
                        .Append( " );" ).NewLine();
                }
                else
                {
                    Debug.Assert( type is Type t && handler.Type == t );
                    ctor.Append( "_typeWriters.Add( " ).AppendTypeOf( handler.Type )
                        .Append( ", (w,o) => " )
                        .OpenBlock();
                    var variableName = "((" + handler.Type.ToCSharpName() + ")o)";
                    handler.GenerateWrite( ctor, variableName, FromPocoDirectory, true );
                    ctor.CloseBlock()
                        .Append( " );" ).NewLine();
                }
            }
        }

        void GenerateObjectWrite()
        {
            PocoDirectory.Append( @"
            internal void WriteObject( System.Text.Json.Utf8JsonWriter w, object o )
            {
                switch( o )
                {
                    case null: w.WriteNullValue(); break;
                    case string v: w.WriteStringValue( v ); break;
                    case int v: w.WriteNumberValue( v ); break;
                    case bool v: w.WriteBooleanValue( v ); break;
                    default:
                        {
                            w.WriteStartArray();
                            var t = o.GetType();
                            if( !_typeWriters.TryGetValue( t, out var writer ) )
                            {
                                throw new System.Text.Json.JsonException( $""Unregistered type '{t.AssemblyQualifiedName}'."" );
                            }
                            writer( w, o );
                            w.WriteEndArray();
                            break;
                        }
                }
            }
            " );
        }

        void GenerateObjectRead()
        {
            PocoDirectory.Append( @"
            internal object ReadObject( ref System.Text.Json.Utf8JsonReader r )
            {
                switch( r.TokenType )
                {
                    case System.Text.Json.JsonTokenType.Null: r.Read(); return null;
                    case System.Text.Json.JsonTokenType.Number: { var v = r.GetInt32(); r.Read(); return v; }
                    case System.Text.Json.JsonTokenType.String: { var v = r.GetString(); r.Read(); return v; }
                    case System.Text.Json.JsonTokenType.False: { r.Read(); return false; }
                    case System.Text.Json.JsonTokenType.True: { r.Read(); return true; }
                    default:
                        {
                            r.Read();
                            var n = r.GetString();
                            r.Read();
                            if( !_typeReaders.TryGetValue( n, out var reader ) )
                            {
                                throw new System.Text.Json.JsonException( $""Unregistered type name '{n}'."" );
                            }
                            return reader( ref r );
                        }
                }
            }
" );
        }
    }
}

