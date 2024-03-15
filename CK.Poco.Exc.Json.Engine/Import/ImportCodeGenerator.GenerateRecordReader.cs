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
                        .Append( " v,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                        .OpenBlock()
                        .GeneratedByComment().NewLine();
            if( r.IsAnonymous )
            {
                GenerateReadAnonymousRecordPocoType( importerType, r );
            }
            else
            {
                GenerateReadNamedRecordPocoType( importerType, r );
            }

            importerType.CloseBlock();

        }

        // Allows either {"object":"syntax"} or ["array","syntax"].
        void GenerateReadAnonymousRecordPocoType( ITypeScope writer, IRecordPocoType type )
        {
            writer.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.StartArray )" )
                  .OpenBlock()
                  .Append( "if( !r.Read() ) rCtx.ReadMoreData( ref r );" ).NewLine();
            foreach( var f in type.Fields )
            {
                if( _nameMap.TypeSet.Contains( f.Type ) )
                {
                    writer.Append( "if( rCtx.RuntimeFilter.Contains( " ).Append( f.Type.Index >> 1 ).Append( ") )" )
                          .OpenBlock();
                    GenerateRecordFieldRead( writer, f, $"v.Item{f.Index + 1}" );
                    writer.CloseBlock()
                          .Append( "else" )
                          .OpenBlock()
                          .Append( "if( !r.TrySkip() ) rCtx.SkipMoreData( ref r );" )
                          .CloseBlock();
                }
            }
            writer.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) r.ThrowJsonException( \"Expecting ']' to end anonymous record '" )
                    .Append( type.CSharpName ).Append( "'.\" );" ).NewLine()
                    .Append( "if( !r.Read() ) rCtx.ReadMoreData( ref r );" );
            writer.CloseBlock()
                  .Append( "else if( r.TokenType == System.Text.Json.JsonTokenType.StartObject )" )
                  .OpenBlock()
                  .Append( "if( !r.Read() ) rCtx.ReadMoreData( ref r );" ).NewLine();
            GenerateReadOfNamedFields( writer, type );
            writer.CloseBlock()
                  .Append( "else r.ThrowJsonException( \"Expecting '[' or '{' to start anonymous record '" )
                    .Append( type.CSharpName ).Append( "'.\" );" ).NewLine();
        }

        void GenerateReadNamedRecordPocoType( ITypeScope writer, IRecordPocoType type )
        {
            writer.Append( "if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) r.ThrowJsonException( \"Expecting '{' to start named record '" )
                    .Append( type.CSharpName ).Append( @"'."" );" ).NewLine()
                    .Append( "if( !r.Read() ) rCtx.ReadMoreData( ref r );" ).NewLine();
            GenerateReadOfNamedFields( writer, type );
        }

        void GenerateReadOfNamedFields( ITypeScope writer, IRecordPocoType type )
        {
            writer.Append( """
                            while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
                            {
                                var n = r.GetString();
                                if( !r.Read() ) rCtx.ReadMoreData( ref r );
                                switch( n )
                                {
                            """ );
            foreach( var f in type.Fields )
            {
                if( _nameMap.TypeSet.Contains( f.Type ) )
                {
                    var camel = JsonNamingPolicy.CamelCase.ConvertName( f.Name );
                    if( camel != f.Name )
                    {
                        writer.Append( "case " ).AppendSourceString( camel ).Append( ":" ).NewLine();
                    }
                    writer.Append( "case " ).AppendSourceString( f.Name ).Append( ":" )
                            .OpenBlock();
                    writer.Append( "if( !rCtx.RuntimeFilter.Contains( " ).Append( f.Type.Index >> 1 ).Append( ") ) goto default;" ).NewLine();
                    GenerateRecordFieldRead( writer, f, $"v.{f.Name}" );
                    writer.Append( "break;" )
                            .CloseBlock();
                }
            }
            writer.Append( """
                                    default:
                                    {
                                        if( !r.TrySkip() ) rCtx.SkipMoreData( ref r );
                                        if( !r.Read() ) rCtx.ReadMoreData( ref r );
                                        break;
                                    }
                                }
                            }
                            """ );
            writer.Append( $$"""
                    if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) r.ThrowJsonException( "Expecting '}' to end record '{{type.CSharpName}}'." );
                    if( !r.Read() ) rCtx.ReadMoreData( ref r );
                    """ );
        }

        void GenerateRecordFieldRead( ITypeScope writer, IRecordPocoField f, string implFieldName )
        {
            if( f.Type.IsPolymorphic )
            {
                writer.Append( implFieldName ).Append( " = (" ).Append( f.Type.ImplTypeName ).Append( ")CK.Poco.Exc.JsonGen.Importer.ReadAny( ref r, rCtx );" );
            }
            else if( f.Type is IRecordPocoType )
            {
                // Struct are not by ref in a struct: use the reader function instead of the ref reader code.
                writer.Append( implFieldName ).Append( " = " ).Append( GetReadFunctionName( f.Type ) ).Append( "( ref r, rCtx );" );
            }
            else
            {
                GenerateRead( writer, f.Type, implFieldName, !f.DefaultValueInfo.RequiresInit );
            }
            writer.NewLine();
        }
    }
}
