using CK.CodeGen;
using CK.Core;
using System.Text.Json;

namespace CK.Setup.PocoJson
{
    sealed partial class ImportCodeGenerator
    {
        void GenerateRecordReader( IActivityMonitor monitor, ITypeScope importerType, IRecordPocoType r )
        {
            importerType.Append( "internal static void Read_" )
                        .Append( r.Index )
                        .Append( "(ref System.Text.Json.Utf8JsonReader r,ref " )
                        .Append( r.ImplTypeName )
                        .Append( " v,CK.Poco.Exc.Json.Import.PocoJsonImportOptions options)" )
                        .OpenBlock()
                        .GeneratedByComment().NewLine();
            if( r.IsAnonymous )
            {
                GenerateReadAnonymousFields( importerType, r );
            }
            else
            {
                GenerateReadNamedFields( importerType, r );
            }

            importerType.CloseBlock();

            void GenerateReadAnonymousFields( ITypeScope writer, IRecordPocoType type )
            {
                writer.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.StartArray ) r.ThrowJsonException( \"Expecting '[' to start anonymous record '" )
                      .Append( type.CSharpName ).Append( "'.\" );" ).NewLine()
                      .Append( "r.Read();" ).NewLine();
                foreach( var f in type.Fields )
                {
                    if( f.IsExchangeable && _nameMap.IsExchangeable( f.Type ) )
                    {
                        GenerateRead( writer, f.Type, $"v.{f.Name}", f.DefaultValueInfo.RequiresInit ? false : null );
                        writer.NewLine();
                    }
                }
            }

            void GenerateReadNamedFields( ITypeScope writer, IRecordPocoType type )
            {
                writer.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) r.ThrowJsonException( \"Expecting '{' to start named record '" )
                      .Append( type.CSharpName ).Append( @"'."" );
r.Read();
while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
{
    var n = r.GetString();
    r.Read();
    switch( n )
    {
" );
                foreach( var f in type.Fields )
                {
                    if( f.IsExchangeable && _nameMap.IsExchangeable( f.Type ) )
                    {
                        var camel = JsonNamingPolicy.CamelCase.ConvertName( f.Name );
                        if( camel != f.Name )
                        {
                            writer.Append( "case " ).AppendSourceString( camel ).Append( ":" ).NewLine();
                        }
                        writer.Append( "case " ).AppendSourceString( f.Name ).Append( ":" )
                              .OpenBlock();
                        GenerateRead( writer, f.Type, $"v.{f.Name}", f.DefaultValueInfo.RequiresInit ? false : null );
                        writer.Append( "break;" )
                              .CloseBlock();
                    }
                }
                writer.Append( @"
        default:
        {
            var t = r.TokenType; 
            if( t == System.Text.Json.JsonTokenType.StartObject || t == System.Text.Json.JsonTokenType.StartArray )
            {
                r.Skip();
            }
            r.Read();
            break;
        }
    }
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) r.ThrowJsonException( ""Expecting '}' to end a Poco."" );
r.Read();" );
            }
        }
    }
}
