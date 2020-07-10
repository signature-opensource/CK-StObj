using CK.CodeGen;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CK.Setup
{
    public partial class PocoJsonSerializerImpl
    {
        readonly Dictionary<Type, string> _names = new Dictionary<Type, string>();

        string GetReadFunctionName( Type t, out bool isNew )
        {
            if( isNew = !_names.TryGetValue( t, out var name ) )
            {
                name = "Read" + _names.Count;
                _names.Add( t, name );
            }
            return name!;
        }

        const string FromPocoDirectory = "this";
        const string FromFactory = "PocoDirectory";
        const string FromPocoClass = "_factory.PocoDirectory";

        /// <summary>
        /// Generates the "public void Read( ref System.Text.Json.Utf8JsonReader r )" method
        /// that handles a potential array definition with a check of the type and the loop
        /// over the properties: the returned part must be filled with the case statements on
        /// the property names.
        /// </summary>
        /// <returns>The part in the switch statement.</returns>
        ITypeScopePart GenerateReadBody()
        {
            PocoClass.Append( "public void Read( ref System.Text.Json.Utf8JsonReader r )" )
              .OpenBlock()
              .Append( @"
bool isDef = r.TokenType == System.Text.Json.JsonTokenType.StartArray;
if( isDef )
{
    r.Read();
    string name = r.GetString();
    if( name != " ).AppendSourceString( PocoInfo.Name );
            if( PocoInfo.PreviousNames.Count > 0 )
            {
                PocoClass.Append( " && !" ).AppendArray( PocoInfo.PreviousNames ).Append( ".Contains( name )" );
            }
            PocoClass.Append( @" )
    {
        throw new System.Text.Json.JsonException( ""Expected '""+ " ).AppendSourceString( PocoInfo.Name ).Append( @" + $""' Poco type, but found '{name}'."" );
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
            var read = PocoClass.CreatePart();
            PocoClass.Append( @"
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
            return read;
        }


        void GenerateAssignation( ICodeWriter read, string variableName, WriteInfo writeInfo, string pocoDirectoryAccessor )
        {
            if( writeInfo.Spec == TypeSpec.Basic )
            {
                GenerateBasicAssignation( read, variableName, writeInfo );
            }
            else
            {
                GenerateComplexPropertyAssignation( read, variableName, writeInfo, pocoDirectoryAccessor );
            }
        }

        string GetCollectionReaderName( WriteInfo info, string pocoDirectoryAccessor )
        {
            var name = GetReadFunctionName( info.Type, out bool isNew );
            if( isNew )
            {
                string cType = info.Type.ToCSharpName();
                var f = PocoDirectory.CreateFunction( "internal void " + name + "( ref System.Text.Json.Utf8JsonReader r, "+ cType + " c )" );
                f.Append( "if( r.TokenType != " )
                .Append( info.Spec == TypeSpec.StringMap ? "System.Text.Json.JsonTokenType.StartObject" : "System.Text.Json.JsonTokenType.StartArray" )
                .Append( @" ) throw new System.Text.Json.JsonException( ""Expected " )
                .Append( info.Spec == TypeSpec.StringMap ? "'{' to start a Json object (dictionary of string)" : "'[' to start a collection" )
                .Append( @"."" );" ).NewLine();

                f.Append( "r.Read();" ).NewLine()
                 .Append( "c.Clear();" ).NewLine()
                 .Append( "while( r.TokenType != " )
                 .Append( info.Spec == TypeSpec.StringMap ? "System.Text.Json.JsonTokenType.EndObject" : "System.Text.Json.JsonTokenType.EndArray" )
                 .Append( " )" )
                 .OpenBlock();

                if( info.Spec == TypeSpec.Map )
                {
                    f.Append( @"if( r.TokenType != System.Text.Json.JsonTokenType.StartArray  ) throw new System.Text.Json.JsonException( ""Expected '[' to start a map item."" );" ).NewLine()
                     .Append( "r.Read();" ).NewLine();

                    f.AppendCSharpName( info.Item1.Type ).Append( " k;" ).NewLine();
                    GenerateAssignation( f, "k", info.Item1, pocoDirectoryAccessor );
                    f.AppendCSharpName( info.Item2.Type ).Append( " v;" ).NewLine();
                    GenerateAssignation( f, "v", info.Item2, pocoDirectoryAccessor );
                    f.Append( "c.Add( k, v );" ).NewLine();

                    f.Append( @"if( r.TokenType != System.Text.Json.JsonTokenType.EndArray  ) throw new System.Text.Json.JsonException( ""Expected ']' to end a map item."" );" ).NewLine()
                     .Append( "r.Read();" ).NewLine();
                }
                else if( info.Spec == TypeSpec.StringMap )
                {
                    f.Append( "string k = r.GetString();" ).NewLine()
                     .Append( "r.Read();" ).NewLine()
                     .AppendCSharpName( info.Item1.Type ).Append( " v;" ).NewLine();
                    GenerateAssignation( f, "v", info.Item1, pocoDirectoryAccessor );
                    f.Append( "c.Add( k, v );" ).NewLine();
                }
                else
                {
                    f.AppendCSharpName( info.Item1.Type ).Append( " v;" ).NewLine();
                    GenerateAssignation( f, "v", info.Item1, pocoDirectoryAccessor );
                    f.Append( "c.Add( v );" ).NewLine();
                }
                f.CloseBlock();
                f.Append( "r.Read();" ).NewLine();
            }
            return name;
        }

        void GenerateComplexPropertyAssignation(
            ICodeWriter read,
            string variableName,
            WriteInfo info,
            string pocoDirectoryAccessor )
        {
            Debug.Assert( info.PocoProperty == null || variableName == info.PocoProperty.PropertyName );
            // For properties, we always implement a setter except if we are auto instantiating the value and NO properties are writable.
            // => An AutoInstantiated property can be null if it has a setter.
            if( info.IsNullable )
            {
                Debug.Assert( info.PocoProperty == null || !info.PocoProperty.AutoInstantiated || info.PocoProperty.HasDeclaredSetter );
                read.Append( "if( r.TokenType == System.Text.Json.JsonTokenType.Null )" )
                    .OpenBlock()
                    .Append( variableName ).Append( " = null;" ).NewLine()
                    .Append( "r.Read();" )
                    .CloseBlock()
                    .Append( "else" )
                    .OpenBlock();
                if( info.Spec != TypeSpec.Basic && info.Spec != TypeSpec.PocoDefiner && info.Spec != TypeSpec.Object )
                {
                    read.Append( "if( " ).Append( variableName ).Append( " == null ) " );
                    PocoSupport.GenerateAutoInstantiatedNewAssignation( read, variableName, info.Type, pocoDirectoryAccessor );
                }
            }
            if( info.Spec == TypeSpec.Poco )
            {
                Debug.Assert( info.PocoType != null );
                read.Append( "((" ).AppendCSharpName( info.PocoType.Root.PocoClass ).Append( ')' ).Append( variableName ).Append( ')' ).Append( ".Read( ref r );" ).NewLine();
            }
            else if( info.Spec == TypeSpec.PocoDefiner )
            {
                // PocoDefiner are always replaced.
                GeneratePocoDefinerAssignation( read, variableName, pocoDirectoryAccessor );
            }
            else if( info.Spec == TypeSpec.Object )
            {
                // Object properties are always replaced.
                GenerateObjectAssignation( read, variableName, pocoDirectoryAccessor );
            }
            else
            {
                Debug.Assert( info.Spec == TypeSpec.List || info.Spec == TypeSpec.Set || info.Spec == TypeSpec.Map || info.Spec == TypeSpec.StringMap );
                GenerateCollectionFill( read, variableName, info, pocoDirectoryAccessor );
            }
            if( info.IsNullable ) read.CloseBlock();
        }

        void GenerateCollectionFill( ICodeWriter read, string variableName, WriteInfo info, string pocoDirectoryAccessor )
        {
            read.Append( pocoDirectoryAccessor ).Append( '.' ).Append( GetCollectionReaderName( info, pocoDirectoryAccessor ) )
                .Append( "( ref r, " ).Append( variableName ).Append( " );" );
        }

        void GenerateObjectAssignation( ICodeWriter read, string variableName, string pocoDirectoryAccessor )
        {
            read.Append( variableName ).Append( " = " ).Append( pocoDirectoryAccessor ).Append( ".ReadObjectValue( ref r );" ).NewLine();
        }

        void GeneratePocoAssignation( ICodeWriter read, ITypeScope tPocoFactory, string variableName, string pocoDirectoryAccessor )
        {
            read.Append( variableName ).Append( " = " ).Append( pocoDirectoryAccessor ).Append( "._f" ).Append( tPocoFactory.UniqueId ).Append( ".Read( ref r );" );
        }

        void GeneratePocoDefinerAssignation( ICodeWriter read, string variableName, string pocoDirectoryAccessor )
        {
            // Funny fact: PocoJsonSerializer is not referenced anywhere:
            // the extension method is not discovered by Roslyn! We must call the static explicitly.
            //read.Append( variableName ).Append( " = " ).Append( pocoDirectoryAccessor ).Append( ".ReadPocoValue( ref r );" ).NewLine();
            read.Append( variableName ).Append( " = PocoJsonSerializer.ReadPocoValue( " ).Append( pocoDirectoryAccessor ).Append( ", ref r );" ).NewLine();
        }

        void GenerateBasicAssignation( ICodeWriter read, string variableName, WriteInfo info )
        {
            read.Append( variableName ).Append( " = " );
            var t = info.Type;
            // Null handling: a prefix does the job.
            if( info.IsNullable )
            {
                read.Append( "r.TokenType == System.Text.Json.JsonTokenType.Null ? null : " );
            }
            if( !ReadNumberValue( read, t ) )
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
            read.Append( ';' ).NewLine()
                .Append( "r.Read();" ).NewLine();
        }

        bool ReadNumberValue( ICodeWriter read, Type t )
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

    }
}
