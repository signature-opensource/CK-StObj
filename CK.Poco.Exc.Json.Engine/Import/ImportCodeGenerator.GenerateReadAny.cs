using CK.CodeGen;
using CK.Core;
using System.Linq;

namespace CK.Setup.PocoJson
{
    sealed partial class ImportCodeGenerator
    {
        void GenerateReadAny()
        {
            // Configures the _typeReaders dictionary in the type initializer.
            var ctor = _importerType.FindOrCreateFunction( "static Importer()" );

            ctor.GeneratedByComment().NewLine()
                .Append( "_anyReaders = new Dictionary<string, ObjectReader>();" ).NewLine();

            foreach( var t in _nameMap.TypeSet.NonNullableTypes.Where( t => t.IsOblivious ) )
            {
                if( t.Kind == PocoTypeKind.Any
                    || t.Kind == PocoTypeKind.AbstractPoco
                    || t.Kind == PocoTypeKind.UnionType ) continue;
                // We cannot directly use the GetReadFunctionName here if the type is a value type: the ReaderFunction
                // here returns an object: it has to be explicitly boxed.
                var typeName = _nameMap.GetName( t );
                var readFunction = GetReadFunctionName( t );
                if( t.Type.IsValueType )
                {
                    ctor.OpenBlock()
                        .Append( "// Type: " ).Append( t.ImplTypeName ).NewLine()
                        .Append( "static object d(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                        .Append( "=>" ).Append( readFunction ).Append( "(ref r, rCtx);" ).NewLine()
                        .Append( "_anyReaders.Add( " ).AppendSourceString( typeName ).Append( ", d );" );
                    bool hasLegacyName = _legacyNameMap.GetName( t ) != typeName;
                    if( hasLegacyName )
                    {
                        ctor.Append( "// Legacy name." ).NewLine()
                            .Append( "_anyReaders.Add( " ).AppendSourceString( _legacyNameMap.GetName( t ) ).Append( ", d );" );
                    }
                    ctor.CloseBlock();
                    // Handle also the nullable value types.
                    var tNull = t.Nullable;
                    typeName = _nameMap.GetName( tNull );
                    readFunction = GetReadFunctionName( tNull );
                    ctor.OpenBlock()
                        .Append( "// Type: " ).Append( tNull.ImplTypeName ).NewLine()
                        .Append( "static object d(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                        .Append( "=>" ).Append( readFunction ).Append( "(ref r, rCtx);" ).NewLine()
                        .Append( "_anyReaders.Add( " ).AppendSourceString( typeName ).Append( ", d );" ).NewLine();
                    if( hasLegacyName )
                    {
                        ctor.Append( "_anyReaders.Add( " ).AppendSourceString( _legacyNameMap.GetName( t ) ).Append( ", d ); // Legacy name." ).NewLine();
                    }
                    ctor.CloseBlock();
                }
                else
                {
                    ctor.Append( "_anyReaders.Add( " ).AppendSourceString( typeName ).Append( "," ).Append( readFunction ).Append( ");" ).NewLine();
                    string legacyName = _legacyNameMap.GetName( t );
                    if( legacyName != typeName )
                    {
                        ctor.Append( "_anyReaders.Add( " ).AppendSourceString( legacyName ).Append( "," ).Append( readFunction ).Append( "); // Legacy name." ).NewLine();
                    }
                }
            }

            _importerType.GeneratedByComment()
                         .Append( @"
static internal readonly object oFalse = false;
static internal readonly object oTrue = true;

internal static object? ReadAny( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )
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
            if( !r.Read() ) rCtx.ReadMoreData( ref r ); // [
            var n = r.GetString();
            if( !r.Read() ) rCtx.ReadMoreData( ref r );
            if( !_anyReaders.TryGetValue( n, out var reader ) )
            {
                r.ThrowJsonException( $""Unregistered type name '{n}'."" );
            }
            if( r.TokenType == System.Text.Json.JsonTokenType.Null )
            {
                o = null;
                if( !r.Read() ) rCtx.ReadMoreData( ref r );
            }
            else
            {
                o = reader( ref r, rCtx );
            }
            if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) r.ThrowJsonException( ""Expected end of 2-cells array."" );
            break;
        }
    }
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    return o;
}
" );

        }
    }
}
