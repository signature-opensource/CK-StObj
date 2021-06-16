using CK.CodeGen;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public partial class UnionTypeSerializationTests
    {
        [ExternalName( "UT" )]
        public interface IAllIntegers : IPoco
        {
            [UnionType]
            object IntOrString { get; set; }

            [UnionType( CanBeExtended = true )]
            object? NullablesOrNot { get; set; }

            [UnionType]
            object AllIntegers { get; set; }

            class UnionTypes
            {
                public (int, string) IntOrString { get; }
                public (int?, int) NullablesOrNot { get; }
                public (sbyte, byte, short, ushort, int, uint, long, ulong, decimal, BigInteger) AllIntegers { get; }
            }
        }

        [TestCase( PocoSerializerMode.Server )]
        [TestCase( PocoSerializerMode.ECMAScriptStandard )]
        [TestCase( PocoSerializerMode.ECMAScriptSafe )]
        public void union_types_with_integers_roundtrip_only_when_read_is_unambiguously_one_of_the_types( PocoSerializerMode mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllIntegers ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();

            var u = services.GetService<IPocoFactory<IAllIntegers>>().Create();

            var options = new PocoJsonSerializerOptions { Mode = mode };
            string? serialized = null;

            var u2 = JsonTestHelper.Roundtrip( directory, u, options, text: t => serialized = t );
            Debug.Assert( serialized != null );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            // Unitialized non nullable reference property is actually null.
            serialized.Should().Be( @"[""UT"",{""IntOrString"":null,""NullablesOrNot"":null,""AllIntegers"":null}]" );

            // This is not ambiguous since only ints are at stake and the big number can obviously only be a BigInteger.
            u.IntOrString = 3712;
            u.NullablesOrNot = 3712;
            u.AllIntegers = BigInteger.Parse( Decimal.MaxValue.ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) + "3712", System.Globalization.NumberFormatInfo.InvariantInfo );

            u2 = JsonTestHelper.Roundtrip( directory, u, options, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            if( mode == PocoSerializerMode.ECMAScriptStandard )
            {
                // In ECMAScriptStandard, "BigInt" is the ECMAScript name of long, ulong, decimal and System.Numerics.BigInteger.
                serialized.Should().Be( @"[""UT"",{""IntOrString"":3712,""NullablesOrNot"":3712,""AllIntegers"":[""BigInt"",""792281625142643375935439503353712""]}]" );
            }
            else
            {
                serialized.Should().Be( @"[""UT"",{""IntOrString"":3712,""NullablesOrNot"":3712,""AllIntegers"":[""BigInteger"",""792281625142643375935439503353712""]}]" );
            }

            // This is not ambiguous.
            u.IntOrString = "a string"; // string is self described.
            u.NullablesOrNot = null; // null resolved of null.
            u.AllIntegers = Decimal.MaxValue; // The Decimal.MaxValue is read as a Decimal.

            u2 = JsonTestHelper.Roundtrip( directory, u, options, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            if( mode == PocoSerializerMode.ECMAScriptStandard )
            {
                serialized.Should().Be( @"[""UT"",{""IntOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""BigInt"",""79228162514264337593543950335""]}]" );
            }
            else if( mode == PocoSerializerMode.ECMAScriptSafe )
            {
                serialized.Should().Be( @"[""UT"",{""IntOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""decimal"",""79228162514264337593543950335""]}]" );
            }
            else
            {
                Debug.Assert( mode == PocoSerializerMode.Server );
                serialized.Should().Be( @"[""UT"",{""IntOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""decimal"",79228162514264337593543950335]}]" );
            }

            // This is not ambiguous: The "best" type for -32000 is a signed 16 bits.
            u.AllIntegers = (short)-32000;
            u2 = JsonTestHelper.Roundtrip( directory, u, options, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            if( mode == PocoSerializerMode.ECMAScriptStandard )
            {
                serialized.Should().Be( @"[""UT"",{""IntOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""Number"",-32000]}]" );
            }
            else
            {
                serialized.Should().Be( @"[""UT"",{""IntOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""short"",-32000]}]" );
            }
        }


        public interface IUnionTypes : IPoco
        {
            [UnionType]
            object IntOrString { get; set; }

            [UnionType]
            object AllIntegers { get; set; }

            [UnionType]
            object ByteOrDouble { get; set; }

            class UnionTypes
            {
                public (int, string) IntOrString { get; }
                public (int?, int) NullablesOrNot { get; }
                public (sbyte, byte, short, ushort, int, uint, long, ulong, decimal, BigInteger) AllIntegers { get; }
                public (byte, double) ByteOrDouble { get; }
            }
        }

        [TestCase( @"{""AllIntegers"":3712}", 3712 )] // Defaults to int.
        [TestCase( @"{""AllIntegers"":[""Number"",-47]}", (sbyte)-47 )]
        [TestCase( @"{""AllIntegers"":[""Number"",-129]}", (short)-129 )]
        [TestCase( @"{""AllIntegers"":[""Number"",32767]}", (short)32767 )]
        [TestCase( @"{""AllIntegers"":[""Number"",-32768]}", (short)-32768 )]
        [TestCase( @"{""AllIntegers"":[""Number"",-32769]}", (int)-32769 )]
        [TestCase( @"{""AllIntegers"":[""Number"",32768]}", (ushort)32768 )]
        [TestCase( @"{""AllIntegers"":[""Number"",65535]}", (ushort)65535 )]
        [TestCase( @"{""AllIntegers"":[""Number"",65536]}", (int)65536 )]
        [TestCase( @"{""AllIntegers"":[""Number"",2147483647]}", (int)2147483647 )]
        [TestCase( @"{""AllIntegers"":[""Number"",2147483648]}", (uint)2147483648 )] // uint
        [TestCase( @"{""AllIntegers"":[""Number"",4294967295]}", (uint)4294967295 )]

        [TestCase( @"{""AllIntegers"":[""Number"",4294967296]}", "Error:InvalidDataException" )] // Above UInt32: quotes are required.
        [TestCase( @"{""AllIntegers"":[""Number"",4294967296]}", "Error:InvalidDataException" )]

        [TestCase( @"{""AllIntegers"":[""BigInt"",4294967296]}", "Error:JsonException" )] // BigInt MUST be a string.
        [TestCase( @"{""AllIntegers"":[""BigInt"",""4294967296""]}", (long)4294967296 )] 
        [TestCase( @"{""AllIntegers"":[""BigInt"",""9223372036854775807""]}", (long)9223372036854775807 )] 
        [TestCase( @"{""AllIntegers"":[""BigInt"",""9223372036854775808""]}", (ulong)9223372036854775808 )] 
        [TestCase( @"{""AllIntegers"":[""BigInt"",""18446744073709551615""]}", (ulong)18446744073709551615 )] 
        [TestCase( @"{""AllIntegers"":[""BigInt"",""79228162514264337593543950335""]}", "Decimal:79228162514264337593543950335" )] // Decimal (max value)
        [TestCase( @"{""AllIntegers"":[""BigInt"",""792281625142643375935439503353712""]}", "BigInteger:792281625142643375935439503353712" )] // Greater than Decimal: BigInteger.

        [TestCase( @"{""ByteOrDouble"":[""Number"",0]}", (byte)0 )]
        [TestCase( @"{""ByteOrDouble"":[""Number"",1]}", (byte)1 )]
        public void reading_values_ECMAScriptStandard_mode( string s, object value )
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IUnionTypes ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;

            object? GetReadValue( IUnionTypes o )
            {
                var pName = s.Split( '"' )[1];
                var p = o.GetType().GetProperty( pName );
                Debug.Assert( p != null );
                return p.GetValue( o )!;
            }

            Type? expectedException = null;
            if( value is string sV )
            {
                if( sV.StartsWith( "BigInteger:", StringComparison.Ordinal ) ) value = BigInteger.Parse( sV.Substring( 11 ), System.Globalization.NumberFormatInfo.InvariantInfo );
                else if( sV.StartsWith( "Decimal:", StringComparison.Ordinal ) ) value = Decimal.Parse( sV.Substring( 8 ), System.Globalization.NumberFormatInfo.InvariantInfo );
                else if( sV.StartsWith( "Error:", StringComparison.Ordinal ) )
                {
                    if( sV.Contains( "JsonException", StringComparison.Ordinal ) ) expectedException = typeof( JsonException );
                    else if( sV.Contains( "InvalidDataException", StringComparison.Ordinal ) ) expectedException = typeof( InvalidDataException );
                    else throw new NotSupportedException();
                }
                else throw new NotSupportedException();
            }

            if( expectedException != null )
            {
                FluentActions.Invoking( () => JsonTestHelper.Deserialize<IUnionTypes>( services, s, new PocoJsonSerializerOptions { Mode = PocoSerializerMode.ECMAScriptStandard } ) )
                             .Should().Throw<Exception>().Which.Should().BeOfType( expectedException );
            }
            else
            {
                var a = JsonTestHelper.Deserialize<IUnionTypes>( services, s, new PocoJsonSerializerOptions { Mode = PocoSerializerMode.ECMAScriptStandard } );
                var read = GetReadValue( a );
                read.Should().Be( value );
                read.Should().BeOfType( value.GetType() );
            }
        }

    }
}
