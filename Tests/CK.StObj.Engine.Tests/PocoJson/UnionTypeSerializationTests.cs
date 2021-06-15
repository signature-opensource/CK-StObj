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
        public interface IUnionTypes : IPoco
        {
            [UnionType]
            object IntOrString { get; set; }

            [UnionType( CanBeExtended = true )]
            object? NullablesOrNot { get; set; }

            [UnionType]
            object BasicTypes { get; set; }

            class UnionTypes
            {
                public (int, string) IntOrString { get; }
                public (int?, int) NullablesOrNot { get; }
                public (sbyte, byte, short, ushort, int, uint, long, ulong, decimal, BigInteger) BasicTypes { get; }
            }
        }

        [TestCase( PocoSerializerMode.Server )]
        [TestCase( PocoSerializerMode.ECMAScriptStandard )]
        [TestCase( PocoSerializerMode.ECMAScriptSafe )]
        public void union_types_roundtrip_only_when_read_is_unambiguously_one_of_the_types( PocoSerializerMode mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IUnionTypes ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();

            var u = services.GetService<IPocoFactory<IUnionTypes>>().Create();

            var options = new PocoJsonSerializerOptions { Mode = mode };
            string? serialized = null;

            var u2 = JsonTestHelper.Roundtrip( directory, u, options, text: t => serialized = t );
            Debug.Assert( serialized != null );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            // Unitialized non nullable reference property is actually null.
            serialized.Should().Be( @"[""UT"",{""IntOrString"":null,""NullablesOrNot"":null,""BasicTypes"":null}]" );

            // This is not ambiguous since only ints are at stake and the big number can obviously only be a BigInteger.
            u.IntOrString = 3712;
            u.NullablesOrNot = 3712;
            u.BasicTypes = BigInteger.Parse( Decimal.MaxValue.ToString( System.Globalization.NumberFormatInfo.InvariantInfo ) + "3712", System.Globalization.NumberFormatInfo.InvariantInfo );

            u2 = JsonTestHelper.Roundtrip( directory, u, options, text: t => serialized = t );
            TestHelper.Monitor.Info( $"IUnionTypes serialization: " + serialized );
            u2.Should().BeEquivalentTo( u );
            serialized.Should().Be( @"" );
        }
    }
}
