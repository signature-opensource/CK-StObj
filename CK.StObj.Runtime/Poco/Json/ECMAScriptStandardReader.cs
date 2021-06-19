using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// This participates in the generic (untyped) read function that is used
    /// to read 'object'.
    /// </summary>
    public abstract class ECMAScriptStandardReader
    {
        protected ECMAScriptStandardReader( string jsonName )
        {
            JsonName = jsonName;
        }

        /// <summary>
        /// Gets the json name.
        /// </summary>
        public string JsonName { get; }

        /// <summary>
        /// Generates the code based on a <see cref="System.Text.Json.Utf8JsonReader"/> variable named "r" and
        /// PocoJsonSerializerOptions variable named "options".
        /// </summary>
        /// <param name="read">The target code.</param>
        public abstract void GenerateRead( ICodeWriter read );

    }

    class ECMAScriptStandardNumberReader : ECMAScriptStandardReader
    {
        public ECMAScriptStandardNumberReader() : base( "Number" )
        {
        }

        public override void GenerateRead( ICodeWriter read )
        {
            read.GeneratedByComment()
                .Append( @"
    // Postel's law: accepting (useless) string.
    double d = r.TokenType == System.Text.Json.JsonTokenType.String
                    ? double.Parse( r.GetString(), System.Globalization.NumberFormatInfo.InvariantInfo )
                    : r.GetDouble();
    r.Read();
    return d;" );
        }
    }

    class ECMAScriptStandardBigIntReader : ECMAScriptStandardReader
    {
        public ECMAScriptStandardBigIntReader() : base( "BigInt" )
        {
        }

        public override void GenerateRead( ICodeWriter read )
        {
            read.GeneratedByComment()
                .Append( @"
    if( r.TokenType != System.Text.Json.JsonTokenType.String ) throw new System.Text.Json.JsonException( $""BigInt input type must be string. Token is '{{r.TokenType}}'."" );
    var s = r.GetString();
    r.Read();
    if( Int64.TryParse( s, System.Globalization.NumberStyles.Integer, System.Globalization.NumberFormatInfo.InvariantInfo, out var l ) )
    {
        return l;
    }
    if( UInt64.TryParse( s, System.Globalization.NumberStyles.Integer, System.Globalization.NumberFormatInfo.InvariantInfo, out var ul ) )
    {
        return ul;
    }
    if( Decimal.TryParse( s, out var d ) )
    {
        return d;
    }
    if( System.Numerics.BigInteger.TryParse( s, System.Globalization.NumberStyles.Integer, System.Globalization.NumberFormatInfo.InvariantInfo, out var b ) )
    {
        return b;
    }
    throw new System.IO.InvalidDataException( ""BigInt input type is invalid. Cannot parse a long, ulong, decimal or BigInteger from: "" + s );" );
        }
    }

}
