using CK.CodeGen;
using CK.Core;
using System.Diagnostics;
using System.Text.Json;

namespace CK.Setup.PocoJson
{

    sealed partial class ImportCodeGenerator
    {
        public void GeneratePocoSupport( IActivityMonitor monitor, ICSCodeGenerationContext ctx, IPrimaryPocoType type )
        {
            var pocoClass = ctx.Assembly.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoClass );
            Debug.Assert( type.FamilyInfo.PocoClass.Name == pocoClass.Name );
            ImplementFactorySupport( monitor, ctx, type, pocoClass.Name );

            // The constructor is a helper that calls the ReadJson method.
            pocoClass.GeneratedByComment()
                     .Append( "public " )
                     .Append( pocoClass.Name )
                     .Append( "(ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.Import.PocoJsonImportOptions options ):this()=>" )
                     .Append( "ReadJson(ref r,options);" )
                     .NewLine();

            pocoClass.Append( "public void ReadJson(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.Import.PocoJsonImportOptions options)" )
                     .OpenBlock()
                     .Append( @"
bool isDef = r.TokenType == System.Text.Json.JsonTokenType.StartArray;
if( isDef )
{
    r.Read();
    string name = r.GetString();
    if( name != " ).AppendSourceString( type.ExternalOrCSharpName );
            if( type.ExternalName != null && type.ExternalName.PreviousNames.Count > 0 )
            {
                pocoClass.Append( " && !" ).AppendArray( type.ExternalName.PreviousNames ).Append( ".Contains( name )" );
            }
            pocoClass.Append( @" )
    {
        r.ThrowJsonException( ""Expected '""+ " ).AppendSourceString( type.ExternalOrCSharpName ).Append( @" + $""' Poco type, but found '{name}'."" );
    }
    r.Read();
}
if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) r.ThrowJsonException( ""Expecting '{' to start Poco '" )
        .Append( type.ExternalOrCSharpName ).Append( @"'."" );
r.Read();
while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
{
    var n = r.GetString();
    r.Read();
    switch( n )
    {
" ).NewLine();
            foreach( var f in type.Fields )
            {
                if( f.IsExchangeable && _nameMap.IsExchangeable( f.Type ) )
                {
                    var camel = JsonNamingPolicy.CamelCase.ConvertName( f.Name );
                    if( camel != f.Name )
                    {
                        pocoClass.Append( "case " ).AppendSourceString( camel ).Append( ":" ).NewLine();
                    }
                    pocoClass.Append( "case " ).AppendSourceString( f.Name ).Append( ":" )
                             .OpenBlock();

                    GenerateRead( pocoClass, f.Type, f.PrivateFieldName, f.DefaultValueInfo.RequiresInit ? false : null );

                    pocoClass.Append("break;").CloseBlock();
                }
            }
            pocoClass.Append( @"
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
r.Read();
if( isDef )
{
    if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) r.ThrowJsonException( ""Expecting ']' to end a Poco array."" );
    r.Read();
}
" ).CloseBlock();

            static void ImplementFactorySupport( IActivityMonitor monitor, ICSCodeGenerationContext ctx, IPrimaryPocoType type, string pocoTypeName )
            {
                var factory = ctx.Assembly.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoFactoryClass );
                foreach( var i in type.FamilyInfo.Interfaces )
                {
                    var readerName = $"PocoJsonImportSupport.IFactoryReader<{i.CSharpName}>";

                    factory.Definition.BaseTypes.Add( new ExtendedTypeName( readerName ) );
                    factory.Append( i.CSharpName ).Space().Append( readerName ).Append( ".Read( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.Import.PocoJsonImportOptions options )" ).NewLine()
                            .Append( " => r.TokenType == System.Text.Json.JsonTokenType.Null ? null : new " )
                            .Append( pocoTypeName ).Append( "( ref r, options );" ).NewLine();

                }
                factory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.Import.PocoJsonImportOptions options ) => new " )
                    .Append( pocoTypeName ).Append( "( ref r, options );" ).NewLine();
            }
        }


    }
}