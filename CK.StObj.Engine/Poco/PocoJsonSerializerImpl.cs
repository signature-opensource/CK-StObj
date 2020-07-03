using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    public class PocoJsonSerializerImpl : ICodeGenerator
    {
        public AutoImplementationResult Implement( IActivityMonitor monitor, ICodeGenerationContext c )
        {
            var poco = c.Assembly.GetPocoSupportResult();

            foreach( var p in poco.Roots )
            {
                var tFactory = c.Assembly.FindOrCreateAutoImplementedClass( monitor, p.PocoFactoryClass );
                foreach( var i in p.Interfaces )
                {
                    var interfaceName = i.PocoInterface.ToCSharpName();
                    var readerName = "CK.Core.PocoJsonSerializer.IFactoryReader<" + interfaceName + ">";

                    tFactory.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( readerName ) );
                    tFactory.Append( interfaceName ).Append( ' ' ).Append( readerName ).Append( ".Read( ref System.Text.Json.Utf8JsonReader r )" ).NewLine()
                            .Append( " => r.TokenType == System.Text.Json.JsonTokenType.Null ? null : new " )
                            .Append( p.PocoClass.Name ).Append( "( ref r );" ).NewLine();

                    tFactory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r ) => new " ).Append( p.PocoClass.Name ).Append( "( ref r );" ).NewLine();
                }
                var tPoco = c.Assembly.FindOrCreateAutoImplementedClass( monitor, p.PocoClass );
                ExtendPoco( tPoco, p, poco );
            }
            return AutoImplementationResult.Success;
        }

        void ExtendPoco( ITypeScope tPoco, IPocoRootInfo root, IPocoSupportResult poco )
        {
            System.Text.Json.Utf8JsonWriter w;

            // Each Poco class is a IWriter and has a constructor that accepts a Utf8JsonReader.
            tPoco.TypeDefinition.BaseTypes.Add( new ExtendedTypeName( "CK.Core.PocoJsonSerializer.IWriter" ) );
            tPoco.Append( "public void Write( System.Text.Json.Utf8JsonWriter w, bool withType )" ).NewLine()
                 .Append( '{' ).NewLine()
                 .Append( "if( withType ) { w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( root.Name ).Append( "); }" ).NewLine()
                 .Append( "w.WriteStartObject();" ).NewLine();
            var write = tPoco.CreatePart();
            tPoco.NewLine()
                 .Append( "w.WriteEndObject();" ).NewLine()
                 .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                 .Append( '}' ).NewLine();

            tPoco.Append( "public " ).Append( tPoco.Name ).Append( "( ref System.Text.Json.Utf8JsonReader r )" ).NewLine()
              .Append( '{' ).NewLine();
            var read = tPoco.CreatePart();
            tPoco.NewLine().Append( '}' ).NewLine();

            // Fill the "read" and "write" parts in one pass.

            // Read prefix: starts the loop on the "PropertyName" Json fields.
            read.Append( @"
if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) throw new System.Text.Json.JsonException( ""Expecting '{' to start a Poco."" );
r.Read();
while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
{
    var n = r.GetString();
    r.Read();
    switch( n )
    {
" );
            // For each property, GenerateWriteForType fills the "write" (this is easy) and
            // returns the non nullable property type and whether it is nullable.
            foreach( var p in root.PropertyList )
            {
                write.Append( "w.WritePropertyName( " ).AppendSourceString( p.PropertyName ).Append( " );" ).NewLine();
                var (isNullable, t) = GenerateWriteForType( tPoco, write, p.PropertyName, p.PropertyType, poco );

            }

            // read suffix: closes the loop and ensures that a '}' is found.
            read.Append( @"
    }
    r.Read();
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) throw new System.Text.Json.JsonException( ""Expecting '}' to end a Poco."" );
r.Read();
" );
        }

        (bool IsNullable, Type Type) GenerateWriteForType( ITypeScope tB, ICodeWriter write, string variableName, Type t, IPocoSupportResult poco )
        {
            bool unsupportedType = false;
            bool isNullable = IsNullable( ref t );
            // Null handling: a prefix does the job.
            if( isNullable )
            {
                write.Append( "if( " ).Append( variableName ).Append( " == null ) w.WriteNullValue();" ).NewLine()
                     .Append( "else " );
            }
            // 
            if( t == typeof( bool ) )
            {
                write.Append( "w.WriteBooleanValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( int )
                     || t == typeof( double )
                     || t == typeof( float )
                     || t == typeof( long )
                     || t == typeof( uint )
                     || t == typeof( byte )
                     || t == typeof( sbyte )
                     || t == typeof( short )
                     || t == typeof( ushort )
                     || t == typeof( ulong )
                     || t == typeof( decimal ) )
            {
                write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( byte[] ) )
            {
                write.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t == typeof( string )
                     || t == typeof( Guid )
                     || t == typeof( DateTime )
                     || t == typeof( DateTimeOffset ) )
            {
                write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
            }
            else if( t.IsEnum )
            {
                var eT = Enum.GetUnderlyingType( t );
                write.Append( "w.WriteNumberValue( (" ).AppendCSharpName( eT ).Append( ')' ).Append( variableName ).Append( " );" );
            }
            else if( t.IsGenericType )
            {
                Type genType = t.GetGenericTypeDefinition();
                bool isList = genType == typeof( IList<> ) || genType == typeof( List<> );
                if( isList || genType == typeof( ISet<> ) || genType == typeof( HashSet<> ) )
                {
                    Type itemType = t.GetGenericArguments()[0];

                    write.Append( "w.WriteStartArray();" ).NewLine()
                         .Append( "foreach( var p in " ).Append( variableName ).Append( " )" ).NewLine()
                         .Append( "{" ).NewLine();

                    //  GenerateWriteForType( tB, write, "p", itemType );

                    write.Append( "}" ).NewLine()
                         .Append( "w.WriteEndArray();" ).NewLine();
                }
                else if( genType == typeof( IDictionary<,> ) || genType == typeof( Dictionary<,> ) )
                {
                    var gArgs = t.GetGenericArguments();
                    var tKey = gArgs[0];
                    var tValue = gArgs[1];
                    if( tKey == typeof( string ) )
                    {
                        write.Append( "w.WriteStartObject();" ).NewLine()
                             .Append( "foreach( var kv in " ).Append( variableName ).Append( " )" ).NewLine()
                             .Append( "{" ).NewLine()
                             .Append( "w.WritePropertyName( kv.Key );" ).NewLine();
                        GenerateWriteForType( tB, write, "kv.Value", tValue, poco );
                        write.Append( "}" ).NewLine()
                             .Append( "w.WriteEndObject();" ).NewLine();
                    }
                    else
                    {
                        write.Append( "w.WriteStartArray();" ).NewLine()
                             .Append( "foreach( var kv in " ).Append( variableName ).Append( " )" ).NewLine()
                             .Append( "{" ).NewLine()
                             .Append( "w.WriteStartArray();" ).NewLine();
                        GenerateWriteForType( tB, write, "kv.Key", tKey, poco );
                        GenerateWriteForType( tB, write, "kv.Value", tValue, poco );
                        write.Append( "w.WriteEndArray();" ).NewLine()
                             .Append( "}" ).NewLine()
                             .Append( "w.WriteEndArray();" ).NewLine();
                    }
                }
                else
                {
                    unsupportedType = true;
                }
            }
            else
            {
                unsupportedType = true;
            }
            if( unsupportedType )
            {
                if( typeof( IPoco ).IsAssignableFrom( t ) )
                {
                    write.Append( variableName );
                    // If its a real Poco type and not a definer, we write its value directly.
                    // When it's only a definer, we write it with its type.
                    bool isPocoDefiner = poco.Find( t ) == null;
                    write.Append( variableName ).Append( "Write( w, " ).Append( isPocoDefiner ).Append( " );" );
                }
            }
            if( unsupportedType )
            {
                throw new InvalidOperationException( $"Json serialization is not supported for type '{t.ToCSharpName()}'." );
            }
            write.NewLine();
            return (isNullable, t);
        }

        static bool IsNullable( ref Type t )
        {
            bool isNullable = t.IsClass || t.IsInterface;
            if( !isNullable )
            {
                Type? tN = Nullable.GetUnderlyingType( t );
                if( tN != null )
                {
                    t = tN;
                    isNullable = true;
                }
            }
            return isNullable;
        }

    }
}
