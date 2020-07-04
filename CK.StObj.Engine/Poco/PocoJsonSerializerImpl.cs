using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                            .Append( p.PocoClass.Name ).Append( "( ref r, PocoDirectory );" ).NewLine();

                    tFactory.Append( "public IPoco ReadTyped( ref System.Text.Json.Utf8JsonReader r ) => new " ).Append( p.PocoClass.Name ).Append( "( ref r, PocoDirectory );" ).NewLine();
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
            tPoco.Append( "public void Write( System.Text.Json.Utf8JsonWriter w, bool withType )" )
                 .OpenBlock()
                 .Append( "if( withType ) { w.WriteStartArray(); w.WriteStringValue( " ).AppendSourceString( root.Name ).Append( "); }" ).NewLine()
                 .Append( "w.WriteStartObject();" ).NewLine();
            var write = tPoco.CreatePart();
            tPoco.NewLine()
                 .Append( "w.WriteEndObject();" ).NewLine()
                 .Append( "if( withType ) w.WriteEndArray();" ).NewLine()
                 .CloseBlock();

            tPoco.Append( "public " ).Append( tPoco.Name ).Append( "( ref System.Text.Json.Utf8JsonReader r, PocoDirectory d )" )
                 .Append( "=> Read( ref r, d );" ).NewLine();

            // Poco has a Read method but it is not exposed.

            tPoco.Append( "public void Read( ref System.Text.Json.Utf8JsonReader r, PocoDirectory d )" )
              .OpenBlock()
              .Append( @"
bool isDef = r.TokenType == System.Text.Json.JsonTokenType.StartArray;
if( isDef )
{
    r.Read();
    string name = r.GetString();
    if( name != " ).AppendSourceString( root.Name )
        .Append( " && !").AppendArray( root.PreviousNames )
        .Append( ".Contains(" ).AppendSourceString( root.Name ) 
        .Append( @" ) )
    {
        throw new System.Text.Json.JsonException( ""Expected '""+ ").AppendSourceString(root.Name).Append( @" + $""' Poco type, but found '{name}'."" );
    }
    r.Read();
}
if( r.TokenType != System.Text.Json.JsonTokenType.StartObject ) throw new System.Text.Json.JsonException( ""Expecting '{' to start a Poco."" );
r.Read();
while( r.TokenType == System.Text.Json.JsonTokenType.PropertyName )
{
    var n = r.GetString();
    r.Read();
    switch( n )
    {
" ).NewLine();
            var read = tPoco.CreatePart();
            tPoco.Append( @"
    }
    r.Read();
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) throw new System.Text.Json.JsonException( ""Expecting '}' to end a Poco."" );
r.Read();
if( isDef )
{
    if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) throw new System.Text.Json.JsonException( ""Expecting ']' to end a Poco array."" );
    r.Read();
}
" ).CloseBlock();

            // Fill the "read" and "write" parts in one pass.

            // Read prefix: starts the loop on the "PropertyName" Json fields.
            // For each property, GenerateWriteForType fills the "write" (this is easy) and
            // returns the non nullable property type and whether it is nullable.
            foreach( var p in root.PropertyList )
            {
                write.Append( "w.WritePropertyName( " ).AppendSourceString( p.PropertyName ).Append( " );" ).NewLine();
                var (isNullable, t) = GenerateWriteForType( tPoco, write, p.PropertyName, p.PropertyType, poco );

                read.Append( "case " ).AppendSourceString( p.PropertyName ).Append( " : " );
                if( p.AutoInstantiated )
                {
                    read.OpenBlock();
                    read.Append( "break; " ).CloseBlock();
                }
                else
                {
                    read.Append( p.PropertyName ).Append( " = " );
                    if( !ReadNumberValue( read, p.PropertyType ) )
                    {
                        if( t == typeof( string ) ) read.Append( "r.GetString()" );
                        else if( t == typeof( bool ) ) read.Append( "r.GetBoolean()" );
                        else if( t == typeof( Guid ) ) read.Append( "r.GetGuid()" );
                        else if( t == typeof( DateTime ) ) read.Append( "r.GetDateTime()" );
                        else if( t == typeof( DateTimeOffset ) ) read.Append( "r.GetDateTimeOffset()" );
                        else if( t == typeof( byte[] ) ) read.Append( "r.GetBytesFromBase64()" );
                        else if( t.IsEnum )
                        {
                            var eT = Enum.GetUnderlyingType( t );
                            read.Append( '(' ).AppendCSharpName( t ).Append( ')' );
                            ReadNumberValue( read, eT );
                        }
                        else
                        {
                            Debug.Fail( $"Unsupported type is already handled by the Write." );
                        }
                    }
                    read.Append( "; break;" ).NewLine();
                }

            }

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

        #region Read

        static bool ReadNumberValue( ICodeWriter read, Type t )
        {
            if( t == typeof( int ) ) read.Append( "r.GetInt32()" );
            else if( t == typeof( double ) ) read.Append( "r.GetDouble()" );
            else if( t == typeof( float ) ) read.Append( "r.GetFloat()" );
            else if( t == typeof( long ) ) read.Append( "r.GetInt64()" );
            else if( t == typeof( uint ) ) read.Append( "r.GetUInt32()" );
            else if( t == typeof( byte ) ) read.Append( "r.GetByte()" );
            else if( t == typeof( sbyte ) ) read.Append( "r.GetSByte()" );
            else if( t == typeof( short ) ) read.Append( "r.GetInt16()" );
            else if( t == typeof( ushort ) ) read.Append( "r.GetUInt16()" );
            else if( t == typeof( ulong ) ) read.Append( "r.GetUInt64()" );
            else if( t == typeof( decimal ) ) read.Append( "r.GetDecimal()" );
            else return false;
            return true;
        }

        #endregion

    }
}
