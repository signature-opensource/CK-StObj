using CK.CodeGen;
using CK.Core;
using System.Diagnostics;
using System.Text.Json;
using static CK.Core.PocoJsonExportSupport;

namespace CK.Setup.PocoJson
{

    sealed partial class ImportCodeGenerator
    {
        public void GeneratePocoSupport( IActivityMonitor monitor, ICSCodeGenerationContext ctx, IPrimaryPocoType type )
        {
            var pocoClass = ctx.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoClass );
            Debug.Assert( type.FamilyInfo.PocoClass.Name == pocoClass.Name );
            ImplementFactorySupport( monitor, ctx, type, pocoClass.Name );

            using var region = pocoClass.Region();

            // The constructor is a helper that calls the ReadJson method.
            pocoClass.Append( "public " )
                     .Append( pocoClass.Name )
                     .Append( "(ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx ):this()=>" )
                     .Append( "ReadJson(ref r,rCtx);" )
                     .NewLine();

            pocoClass.Append( "public void ReadJson(ref System.Text.Json.Utf8JsonReader r,CK.Poco.Exc.Json.PocoJsonReadContext rCtx)" )
                     .OpenBlock()
                     .Append( @"
bool isDef = r.TokenType == System.Text.Json.JsonTokenType.StartArray;
if( isDef )
{
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
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
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
}
if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) r.ThrowJsonException( ""Expecting '{' to start Poco '" )
        .Append( type.ExternalOrCSharpName ).Append( @"'."" );
if( !r.Read() ) rCtx.ReadMoreData( ref r );
while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
{
    var n = r.GetString();
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
    switch( n )
    {
" ).NewLine();
            foreach( var f in type.Fields )
            {
                if( f.FieldAccess != PocoFieldAccessKind.AbstractReadOnly && _nameMap.TypeSet.Contains( f.Type ) )
                {
                    var camel = JsonNamingPolicy.CamelCase.ConvertName( f.Name );
                    if( camel != f.Name )
                    {
                        pocoClass.Append( "case " ).AppendSourceString( camel ).Append( ":" ).NewLine();
                    }
                    pocoClass.Append( "case " ).AppendSourceString( f.Name ).Append( ":" )
                             .OpenBlock();

                    pocoClass.Append( "if( !rCtx.CanImport( " ).Append( f.Type.Index >> 1 ).Append( ") ) goto default;" ).NewLine();
                    GenerateRead( pocoClass, f.Type, f.PrivateFieldName, !f.DefaultValueInfo.RequiresInit );

                    pocoClass.Append("break;")
                             .CloseBlock();
                }
            }
            pocoClass.Append( @"
        default:
        {
            if( !r.TrySkip() ) rCtx.SkipMoreData( ref r );
            if( !r.Read() ) rCtx.ReadMoreData( ref r );
            break;
        }
    }
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) r.ThrowJsonException( ""Expecting '}' to end a Poco."" );
if( !r.Read() ) rCtx.ReadMoreData( ref r );
if( isDef )
{
    if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) r.ThrowJsonException( ""Expecting ']' to end a Poco array."" );
    if( !r.Read() ) rCtx.ReadMoreData( ref r );
}
" ).CloseBlock();

            static void ImplementFactorySupport( IActivityMonitor monitor, ICSCodeGenerationContext ctx, IPrimaryPocoType type, string pocoTypeName )
            {
                var factory = ctx.Assembly.Code.Global.FindOrCreateAutoImplementedClass( monitor, type.FamilyInfo.PocoFactoryClass );
                foreach( var i in type.FamilyInfo.Interfaces )
                {
                    var readerName = $"PocoJsonImportSupport.IFactoryReader<{i.CSharpName}>";

                    factory.Definition.BaseTypes.Add( new ExtendedTypeName( readerName ) );
                    factory.Append( i.CSharpName ).Space().Append( readerName ).Append( ".Read( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx )" ).NewLine()
                            .Append( " => r.TokenType == System.Text.Json.JsonTokenType.Null ? null : new " )
                            .Append( pocoTypeName ).Append( "( ref r, rCtx );" ).NewLine();

                }
                factory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r, CK.Poco.Exc.Json.PocoJsonReadContext rCtx ) => new " )
                    .Append( pocoTypeName ).Append( "( ref r, rCtx );" ).NewLine();
            }
        }


    }
}
