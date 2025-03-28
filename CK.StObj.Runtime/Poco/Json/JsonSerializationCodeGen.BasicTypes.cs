using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CK.Setup.Json
{

    public partial class JsonSerializationCodeGen
    {
        void InitializeBasicTypes()
        {
            Debug.Assert( _typeInfoRefTypeStartIdx == 0 );
            // Direct types.
            AllowTypeInfo( JsonTypeInfo.ObjectType ).Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "PocoDirectory_CK.Write( " ).Append( variableName ).Append( ", options );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = PocoDirectory_CK.ReadObject( ref r, options );" );
                } );

            static void WriteString( ICodeWriter write, string variableName )
            {
                write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( " );" );
            }

            static void WriteNumber( ICodeWriter write, string variableName )
            {
                write.Append( "w.WriteNumberValue( " ).Append( variableName ).Append( " );" );
            }

            // Currently default format is ok but when BigInteger will be handled like any other
            // long by the reader/writer then we'll need the "R" format for it (unless the https://github.com/dotnet/runtime/issues/54016
            // is resolved).
            // ==> We keep the function factory here for the moment.
            static CodeWriter WriteECMAScripSafeNumber( string? toStringFormat = null )
            {
                return ( write, variableName ) =>
                {
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".ToString( " );
                    if( toStringFormat != null )
                    {
                        write.AppendSourceString( toStringFormat ).Append( ", " );
                    }
                    write.Append( "System.Globalization.NumberFormatInfo.InvariantInfo ) );" ).NewLine();
                };
            }

            AllowTypeInfo( typeof( string ), "string" )!.Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetString(); r.Read();" );
                }
                );
            AllowTypeInfo( typeof( bool ), "bool" )!.Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "w.WriteBooleanValue( " ).Append( variableName ).Append( " );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetBoolean(); r.Read();" );
                } );
            AllowTypeInfo( typeof( int ), "int" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt32(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( byte[] ), "byte[]" )!.Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    write.Append( "w.WriteBase64StringValue( " ).Append( variableName ).Append( " );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetBytesFromBase64(); r.Read();" );
                } );

            AllowTypeInfo( typeof( Guid ), "Guid" )!.Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetGuid(); r.Read();" );
                } );

            AllowTypeInfo( typeof( decimal ), "decimal" )!.Configure( WriteECMAScripSafeNumber(),
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    // Instead of challenging the options, let's challenge the data itself and apply Postel's law (see https://en.wikipedia.org/wiki/Robustness_principle).
                    read.Append( variableName ).Append( " = r.TokenType == System.Text.Json.JsonTokenType.String ? Decimal.Parse( r.GetString(), System.Globalization.NumberFormatInfo.InvariantInfo ) : r.GetDecimal(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "BigInt", false );

            AllowTypeInfo( typeof( uint ), "uint" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt32(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( double ), "double" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDouble(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", true );

            AllowTypeInfo( typeof( float ), "float" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSingle(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( long ), "long" )!.Configure( WriteECMAScripSafeNumber(),
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    // Instead of challenging the options, let's challenge the data itself and apply Postel's law (see https://en.wikipedia.org/wiki/Robustness_principle).
                    read.Append( variableName ).Append( " = r.TokenType == System.Text.Json.JsonTokenType.String ? Int64.Parse( r.GetString(), System.Globalization.NumberFormatInfo.InvariantInfo ) : r.GetInt64(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "BigInt", true );

            AllowTypeInfo( typeof( ulong ), "ulong" )!.Configure( WriteECMAScripSafeNumber(),
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    // Instead of challenging the options, let's challenge the data itself and apply Postel's law (see https://en.wikipedia.org/wiki/Robustness_principle).
                    read.Append( variableName ).Append( " = r.TokenType == System.Text.Json.JsonTokenType.String ? UInt64.Parse( r.GetString(), System.Globalization.NumberFormatInfo.InvariantInfo ) :  r.GetUInt64(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "BigInt", false );

            AllowTypeInfo( typeof( byte ), "byte" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetByte(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( sbyte ), "sbyte" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetSByte(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( short ), "short" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetInt16(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( ushort ), "ushort" )!.Configure( WriteNumber,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetUInt16(); r.Read();" );
                } )
                .SetECMAScriptStandardName( "Number", false );

            AllowTypeInfo( typeof( System.Numerics.BigInteger ), "BigInteger" )!.Configure(
                ( ICodeWriter write, string variableName ) =>
                {
                    // Use the BigInteger.ToString(String) method with the "R" format specifier to generate the string representation of the BigInteger value.
                    // Otherwise, the string representation of the BigInteger preserves only the 50 most significant digits of the original value, and data may
                    // be lost when you use the Parse method to restore the BigInteger value.
                    write.Append( "w.WriteStringValue( " ).Append( variableName ).Append( ".ToString( \"R\", System.Globalization.NumberFormatInfo.InvariantInfo ) );" );
                },
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = System.Numerics.BigInteger.Parse( r.GetString(), System.Globalization.NumberFormatInfo.InvariantInfo ); r.Read();" );
                } )
                .SetECMAScriptStandardName( "BigInt", false );

            AllowTypeInfo( typeof( DateTime ), "DateTime" )!.Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTime(); r.Read();" );
                } );

            AllowTypeInfo( typeof( DateTimeOffset ), "DateTimeOffset" )!.Configure( WriteString,
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = r.GetDateTimeOffset(); r.Read();" );
                } );

            AllowTypeInfo( typeof( TimeSpan ), "TimeSpan" )!.Configure(
                ( ICodeWriter write, string variableName ) => WriteECMAScripSafeNumber()( write, variableName + ".Ticks" ),
                ( ICodeWriter read, string variableName, bool assignOnly, bool isNullable ) =>
                {
                    read.Append( variableName ).Append( " = TimeSpan.FromTicks( r.TokenType == System.Text.Json.JsonTokenType.String ? Int64.Parse( r.GetString(), System.Globalization.NumberFormatInfo.InvariantInfo ) : r.GetInt64() ); r.Read();" );
                } );

            _standardReaders.Add( new ECMAScriptStandardNumberReader() );
            _standardReaders.Add( new ECMAScriptStandardBigIntReader() );
            _typeInfoRefTypeStartIdx = _typeInfos.Count;
        }
    }
}
