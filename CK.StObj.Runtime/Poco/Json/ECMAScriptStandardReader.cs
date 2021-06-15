using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    public abstract class ECMAScriptStandardReader
    {
        protected ECMAScriptStandardReader( string name )
        {
            Name = name;
        }

        /// <summary>
        /// Gets the exported name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Generates the code based on a <see cref="System.Text.Json.Utf8JsonReader"/> variable named "r" and
        /// PocoJsonSerializerOptions variable named "options".
        /// </summary>
        /// <param name="read">The target code.</param>
        public abstract void GenerateRead( ICodeWriter read );

        public static object BestCast( double d )
        {
            if( (d % 1) == 0 )
            {
                // It is an integer.
                if( d < 0 )
                {
                    // Negative integer.
                    if( d < Int32.MinValue ) return d;
                    if( d < Int16.MinValue ) return (int)d;
                    if( d < SByte.MinValue ) return (short)d;
                    return (sbyte)d;
                }
                // Positive integer.
                if( d > UInt32.MaxValue ) return d;
                if( d > Int32.MaxValue ) return (uint)d;
                if( d > UInt16.MaxValue ) return (int)d;
                if( d > Int16.MaxValue ) return (ushort)d;
                if( d > Byte.MaxValue ) return (short)d;
                if( d > SByte.MaxValue ) return (byte)d;
                return (sbyte)d;
            }
            return d;
        }


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
    if( (d % 1) == 0 ) // This tests whether the double has a fractional part.
    {
        // It is an integer.
        if( d < 0 )
        {
            // Negative integer.
            if( d < Int32.MinValue ) return d;
            if( d < Int16.MinValue ) return (int)d;
            if( d < SByte.MinValue ) return (short)d;
            return (sbyte)d;
        }
        // Positive integer.
        if( d > UInt32.MaxValue ) return d;
        if( d > Int32.MaxValue ) return (uint)d;
        if( d > UInt16.MaxValue ) return (int)d;
        if( d > Int16.MaxValue ) return (ushort)d;
        if( d > Byte.MaxValue ) return (short)d;
        if( d > SByte.MaxValue ) return (byte)d;
        return (sbyte)d;
    }
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
    return System.Numerics.BigInteger.Parse( s, System.Globalization.NumberFormatInfo.InvariantInfo );" );
        }
    }

}
