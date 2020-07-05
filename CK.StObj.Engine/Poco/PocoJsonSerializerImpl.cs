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
                ExtendPoco( tFactory, tPoco, p, poco );
            }
            return AutoImplementationResult.Success;
        }

        void ExtendPoco( ITypeScope tPocoFactory, ITypeScope tPoco, IPocoRootInfo root, IPocoSupportResult poco )
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

            tPoco.Append( "public " ).Append( tPoco.Name ).Append( "( ref System.Text.Json.Utf8JsonReader r, PocoDirectory_CK d )" )
                 .OpenBlock()
                 .Append( "_factory = d._f" ).Append( tPocoFactory.UniqueId ).Append( ';' ).NewLine();
            var ctor = tPoco.CreatePart();
            tPoco.Append( "Read( ref r );" )
                 .CloseBlock();

            // Poco has a Read method but it is not exposed.

            tPoco.Append( "public void Read( ref System.Text.Json.Utf8JsonReader r )" )
              .OpenBlock()
              .Append( @"
bool isDef = r.TokenType == System.Text.Json.JsonTokenType.StartArray;
if( isDef )
{
    r.Read();
    string name = r.GetString();
    if( name != " ).AppendSourceString( root.Name );
            if( root.PreviousNames.Count > 0 )
            {
                tPoco.Append( " && !" ).AppendArray( root.PreviousNames ).Append( ".Contains( name )" );
            }
            tPoco.Append( @" )
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
}
if( r.TokenType != System.Text.Json.JsonTokenType.EndObject ) throw new System.Text.Json.JsonException( ""Expecting '}' to end a Poco."" );
r.Read();
if( isDef )
{
    if( r.TokenType != System.Text.Json.JsonTokenType.EndArray ) throw new System.Text.Json.JsonException( ""Expecting ']' to end a Poco array."" );
    r.Read();
}
" ).CloseBlock();

            // Fill the "read" and "write" parts in one pass as well as the "ctor" where
            // auto instantiated properties that have no declared setter are new'ed.

            // Read prefix: starts the loop on the "PropertyName" Json fields.
            // For each property, GenerateWriteForType fills the "write" (this is easy) and
            // returns the non nullable property type and whether it is nullable.
            foreach( var p in root.PropertyList )
            {
                write.Append( "w.WritePropertyName( " ).AppendSourceString( p.PropertyName ).Append( " );" ).NewLine();
                var (isNullable, t, typeSpec, pocoType) = GenerateWriteForType( tPoco, write, p.PropertyName, p.PropertyType, poco );

                read.Append( "case " ).AppendSourceString( p.PropertyName ).Append( " : " );
                if( typeSpec != TypeSpec.None )
                {
                    read.OpenBlock();
                    bool mayBeNull = !p.AutoInstantiated || p.HasDeclaredSetter;
                    if( !mayBeNull )
                    {
                        poco.WriteAutoInstantiatedProperty( ctor, p, "d" );
                    }
                    else
                    {
                        read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" ).OpenBlock()
                            .Append( p.PropertyName ).Append( " = null;" ).NewLine()
                            .Append( "r.Read();" )
                            .CloseBlock()
                            .Append( "else" )
                            .OpenBlock();
                        if( p.AutoInstantiated )
                        {
                            read.Append( "if( " ).Append( p.PropertyName ).Append( " == null ) " );
                            poco.WriteAutoInstantiatedProperty( read, p, "_factory.PocoDirectory" );
                        }
                    }
                    if( typeSpec == TypeSpec.Poco )
                    {
                        if( pocoType != null )
                        {
                            if( !p.AutoInstantiated )
                            {
                                read.Append( "if( " ).Append( p.PropertyName ).Append( " != null ) " );
                            }
                            read.Append( "((" ).AppendCSharpName( pocoType.Root.PocoClass ).Append( ')' ).Append( p.PropertyName ).Append( ')' ).Append( ".Read( ref r );" ).NewLine();
                            if( !p.AutoInstantiated )
                            {
                                read.Append( "else" ).OpenBlock()
                                    .Append( p.PropertyName ).Append( " = " )
                                    .Append( "_factory.PocoDirectory._f" ).Append( tPocoFactory.UniqueId ).Append( ".Read( ref r );" )
                                    .CloseBlock();
                            }
                        }
                        else
                        {
                            read.Append( p.PropertyName ).Append( " = _factory.PocoDirectory.ReadPocoValue( ref r );" ).NewLine();
                        }
                    }
                    if( mayBeNull ) read.CloseBlock();
                    read.Append( "break; " )
                        .CloseBlock();
                }
                else
                {
                    read.Append( p.PropertyName ).Append( " = " );
                    // Null handling: a prefix does the job.
                    if( isNullable )
                    {
                        read.Append( "r.TokenType == System.Text.Json.JsonTokenType.Null ? null : " );
                    }
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
                    read.Append( ";" ).NewLine()
                        .Append( "r.Read();" ).NewLine()
                        .Append( "break;" ).NewLine();
                }

            }
        }

        enum TypeSpec
        {
            None,
            Poco,
        }

        (bool IsNullable, Type Type, TypeSpec spec, IPocoInterfaceInfo? pocoType) GenerateWriteForType( ITypeScope tB, ICodeWriter write, string variableName, Type t, IPocoSupportResult poco )
        {
            TypeSpec typeSpec = TypeSpec.None;
            IPocoInterfaceInfo? pocoType = null;
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
                             .OpenBlock()
                             .Append( "w.WritePropertyName( kv.Key );" ).NewLine();
                        GenerateWriteForType( tB, write, "kv.Value", tValue, poco );
                        write.CloseBlock()
                             .Append( "w.WriteEndObject();" ).NewLine();
                    }
                    else
                    {
                        write.Append( "w.WriteStartArray();" ).NewLine()
                             .Append( "foreach( var kv in " ).Append( variableName ).Append( " )" ).NewLine()
                             .OpenBlock()
                             .Append( "w.WriteStartArray();" ).NewLine();
                        GenerateWriteForType( tB, write, "kv.Key", tKey, poco );
                        GenerateWriteForType( tB, write, "kv.Value", tValue, poco );
                        write.Append( "w.WriteEndArray();" ).NewLine()
                             .CloseBlock()
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
                    pocoType = poco.Find( t );
                    bool isPocoDefiner = pocoType == null;
                    write.Append( ".Write( w, " ).Append( isPocoDefiner ).Append( " );" );
                    typeSpec = TypeSpec.Poco;
                    unsupportedType = false;
                }
            }
            if( unsupportedType )
            {
                throw new InvalidOperationException( $"Json serialization is not supported for type '{t.ToCSharpName()}'." );
            }
            write.NewLine();
            return (isNullable, t, typeSpec, pocoType);
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
