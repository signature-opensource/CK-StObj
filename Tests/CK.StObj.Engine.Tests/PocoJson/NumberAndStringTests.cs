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
    public partial class NumberAndStringTests
    {
        [ExternalName( "BasicTypes" )]
        public interface IAllBasicTypes : IPoco
        {
            byte Byte { get; set; }
            sbyte SByte { get; set; }
            short Short { get; set; }
            ushort UShort { get; set; }
            int Integer { get; set; }
            uint UInteger { get; set; }
            long Long { get; set; }
            ulong ULong { get; set; }
            float Float { get; set; }
            double Double { get; set; }
            decimal Decimal { get; set; }
            BigInteger BigInt { get; set; }
            DateTime DateTime { get; set; }
            DateTimeOffset DateTimeOffset { get; set; }
            TimeSpan TimeSpan { get; set; }
            Guid Guid { get; set; }
        }

        [Test]
        public void all_basic_types_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllBasicTypes ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var nMax = s.GetService<IPocoFactory<IAllBasicTypes>>().Create();
            nMax.Byte = Byte.MaxValue;
            nMax.SByte = SByte.MaxValue;
            nMax.Short = Int16.MaxValue;
            nMax.UShort = UInt16.MaxValue;
            nMax.Integer = Int32.MaxValue;
            nMax.UInteger = UInt32.MaxValue;
            nMax.Long = Int64.MaxValue;
            nMax.ULong = UInt64.MaxValue;
            nMax.Float = Single.MaxValue;
            nMax.Double = Double.MaxValue;
            nMax.Decimal = Decimal.MaxValue;
            nMax.BigInt = BigInteger.Parse( "12345678901234567890123456789012345678901234567890123456789012345678901234567890" );
            nMax.DateTime = Util.UtcMaxValue;
            nMax.DateTimeOffset = DateTimeOffset.MaxValue;
            nMax.TimeSpan = TimeSpan.MaxValue;
            nMax.Guid = Guid.Parse( "ffffffff-ffff-ffff-ffff-ffffffffffff" );

            var nMin = s.GetService<IPocoFactory<IAllBasicTypes>>().Create();
            nMin.Byte = Byte.MinValue;
            nMin.SByte = SByte.MinValue;
            nMin.Short = Int16.MinValue;
            nMin.UShort = UInt16.MinValue;
            nMin.Integer = Int32.MinValue;
            nMin.UInteger = UInt32.MinValue;
            nMin.Long = Int64.MinValue;
            nMin.ULong = UInt64.MinValue;
            nMin.Float = Single.MinValue;
            nMin.Double = Double.MinValue;
            nMin.Decimal = Decimal.MinValue;
            nMin.BigInt = BigInteger.Parse( "-12345678901234567890123456789012345678901234567890123456789012345678901234567890" );
            nMin.DateTime = Util.UtcMinValue;
            nMin.DateTimeOffset = DateTimeOffset.MinValue;
            nMin.TimeSpan = TimeSpan.MinValue;
            nMin.Guid = Guid.Empty;

            var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, text => TestHelper.Monitor.Info( $"INumerics(max) serialization: " + text ) );
            nMax2.Should().BeEquivalentTo( nMax );

            var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text => TestHelper.Monitor.Info( $"INumerics(min) serialization: " + text ) );
            nMin2.Should().BeEquivalentTo( nMin );
        }

    }
}
