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
        void FillDynamicMaps( Dictionary<object, IHandler> map )
        {
            PocoDirectory.Append( @"
            delegate object ReaderFunction( ref System.Text.Json.Utf8JsonReader r );
            delegate void WriterFunction( System.Text.Json.Utf8JsonWriter w, object o );

            static readonly Dictionary<string, ReaderFunction> _typeReaders = new Dictionary<string, ReaderFunction>();
            static readonly Dictionary<Type, WriterFunction> _typeWriters = new Dictionary<Type, WriterFunction>();" ).NewLine();

            var ctor = PocoDirectory.FindOrCreateFunction( "public PocoDirectory_CK()" );
            foreach( var (type,handler) in map )
            {
                // Skips direct types that are handled...directly.
                if( handler.Info.DirectType != DirectType.None && handler.Info.DirectType != DirectType.Untyped ) continue;

                // Write is called on o.GetType(): this is the concrete type, it is useless to map an abstract type.
                // And since Read reads back what has been written...
                if( handler.IsAbstractType ) continue;

                if( type is string name )
                {
                    Debug.Assert( name == handler.Name );
                    WriteTypeRead( ctor, handler );
                    if( handler.Type.IsValueType )
                    {
                        // Nullable names are not registered.
                        Debug.Assert( !handler.IsNullable );
                        WriteTypeRead( ctor, handler.Info.NullHandler );
                    }
                }
                else
                {
                    Debug.Assert( type is Type t && handler.Type == t );
                    ctor.Append( "_typeWriters.Add( " ).AppendTypeOf( handler.Type )
                        .Append( ", (w,o) => " )
                        .OpenBlock();
                    // Writing a null object is handled directly by the WriteObject code,
                    // If the Type is a Nullable<>, it must be written with the nullable
                    // marker (the '?' suffix) but we can skip the "if( variableName == null )" block.
                    Type notNullableType = handler.IsNullable && handler.Type.IsValueType
                                            ? handler.Info.NotNullHandler.Type
                                            : handler.Type;
                    handler.GenerateWrite( ctor, "((" + notNullableType.ToCSharpName() + ")o)", true, true );
                    ctor.CloseBlock()
                        .Append( " );" ).NewLine();
                }
            }

            static void WriteTypeRead( IFunctionScope ctor, IHandler handler )
            {
                // Reading null is already handled: we can skip the "if( variableName == null )" block.
                ctor.Append( "_typeReaders.Add( " )
                    .AppendSourceString( handler.Name )
                    .Append( ", delegate( ref System.Text.Json.Utf8JsonReader r )" )
                    .OpenBlock()
                    .AppendCSharpName( handler.Type ).Append( " o;" ).NewLine();
                handler.GenerateRead( ctor, "o", true, true );
                ctor.NewLine().Append( "return o;" )
                    .CloseBlock()
                    .Append( " );" ).NewLine();
            }
        }

        void GenerateObjectWrite()
        {
            PocoDirectory.Append( @"
            internal static void WriteObject( System.Text.Json.Utf8JsonWriter w, object o )
            {
                switch( o )
                {
                    case null: w.WriteNullValue(); break;
                    case string v: w.WriteStringValue( v ); break;
                    case int v: w.WriteNumberValue( v ); break;
                    case bool v: w.WriteBooleanValue( v ); break;
                    default:
                        {
                            var t = o.GetType();
                            if( !_typeWriters.TryGetValue( t, out var writer ) )
                            {
                                throw new System.Text.Json.JsonException( $""Unregistered type '{t.AssemblyQualifiedName}'."" );
                            }
                            writer( w, o );
                            break;
                        }
                }
            }
            " );
        }

        void GenerateObjectRead()
        {
            PocoDirectory.Append( @"
            internal static object ReadObject( ref System.Text.Json.Utf8JsonReader r )
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
                            var o = reader( ref r );
                            r.Read();
                            return o;
                        }
                }
            }
" );
        }
    }
}

