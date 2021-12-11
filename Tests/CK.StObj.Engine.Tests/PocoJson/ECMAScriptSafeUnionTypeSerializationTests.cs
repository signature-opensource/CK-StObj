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
    public partial class ECMAScriptSafeUnionTypeSerializationTests
    {
        [ExternalName("NotStandardCompliant")]
        public interface IAllIntegers : IPoco
        {
            [UnionType]
            [DefaultValue("Hop!")]
            object DoubleOrString { get; set; }

            [UnionType( CanBeExtended = true )]
            object? NullablesOrNot { get; set; }

            [UnionType]
            [DefaultValue((byte)87)]
            object AllIntegers { get; set; }

            class UnionTypes
            {
                public (double, string) DoubleOrString { get; }
                public (int, byte)? NullablesOrNot { get; }
                public (sbyte, byte, short, ushort, int, uint, long, ulong, decimal, BigInteger) AllIntegers { get; }
            }
        }

        [Test]
        public void all_integer_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllIntegers ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetRequiredService<PocoDirectory>();

            var u = services.GetRequiredService<IPocoFactory<IAllIntegers>>().Create();

            string? serialized = null;

            var u2 = JsonTestHelper.Roundtrip( directory, u, null, text: t => serialized = t );
            Debug.Assert( serialized != null );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            // Default values are applied..
            serialized.Should().Be( @"[""NotStandardCompliant"",{""DoubleOrString"":""Hop!"",""NullablesOrNot"":null,""AllIntegers"":[""byte"",87]}]" );

            u.DoubleOrString = 3712.0;
            u.NullablesOrNot = 3712;
            u.AllIntegers = BigInteger.Parse( Decimal.MaxValue.ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) + "3712", System.Globalization.NumberFormatInfo.InvariantInfo );

            u2 = JsonTestHelper.Roundtrip( directory, u, null, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            serialized.Should().Be( @"[""NotStandardCompliant"",{""DoubleOrString"":3712,""NullablesOrNot"":[""int"",3712],""AllIntegers"":[""BigInteger"",""792281625142643375935439503353712""]}]" );

            u.DoubleOrString = "a string";
            u.NullablesOrNot = null; // null resolved of null.
            u.AllIntegers = Decimal.MaxValue; // The Decimal.MaxValue is read as a Decimal.

            u2 = JsonTestHelper.Roundtrip( directory, u, null, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            serialized.Should().Be( @"[""NotStandardCompliant"",{""DoubleOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""decimal"",""79228162514264337593543950335""]}]" );

            u.AllIntegers = (short)-32000;
            u2 = JsonTestHelper.Roundtrip( directory, u, null, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            serialized.Should().Be( @"[""NotStandardCompliant"",{""DoubleOrString"":""a string"",""NullablesOrNot"":null,""AllIntegers"":[""short"",-32000]}]" );
        }

    }
}
