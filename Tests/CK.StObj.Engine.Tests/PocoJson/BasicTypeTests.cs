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
    public partial class BasicTypeTests
    {
        [ExternalName( "BasicTypes" )]
        public interface IAllBasicTypes : IPoco
        {
            byte PByte { get; set; }
            sbyte PSByte { get; set; }
            short PShort { get; set; }
            ushort PUShort { get; set; }
            int PInteger { get; set; }
            uint PUInteger { get; set; }
            long PLong { get; set; }
            ulong PULong { get; set; }
            float PFloat { get; set; }
            double PDouble { get; set; }
            decimal PDecimal { get; set; }
            BigInteger PBigInteger { get; set; }
            DateTime PDateTime { get; set; }
            DateTimeOffset PDateTimeOffset { get; set; }
            TimeSpan PTimeSpan { get; set; }
            Guid PGuid { get; set; }
        }

        [TestCase( PocoJsonSerializerMode.ECMAScriptSafe )]
        [TestCase( PocoJsonSerializerMode.ECMAScriptStandard )]
        public void all_basic_types_roundtrip( PocoJsonSerializerMode mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllBasicTypes ) ); ;
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();

            var nMax = services.GetService<IPocoFactory<IAllBasicTypes>>().Create();
            nMax.PByte = Byte.MaxValue;
            nMax.PSByte = SByte.MaxValue;
            nMax.PShort = Int16.MaxValue;
            nMax.PUShort = UInt16.MaxValue;
            nMax.PInteger = Int32.MaxValue;
            nMax.PUInteger = UInt32.MaxValue;
            nMax.PLong = Int64.MaxValue;
            nMax.PULong = UInt64.MaxValue;
            nMax.PFloat = Single.MaxValue;
            nMax.PDouble = Double.MaxValue;
            nMax.PDecimal = Decimal.MaxValue;
            nMax.PBigInteger = BigInteger.Parse( "12345678901234567890123456789012345678901234567890123456789012345678901234567890", System.Globalization.NumberFormatInfo.InvariantInfo );
            nMax.PDateTime = Util.UtcMaxValue;
            nMax.PDateTimeOffset = DateTimeOffset.MaxValue;
            nMax.PTimeSpan = TimeSpan.MaxValue;
            nMax.PGuid = Guid.Parse( "ffffffff-ffff-ffff-ffff-ffffffffffff" );

            var nMin = services.GetService<IPocoFactory<IAllBasicTypes>>().Create();
            nMin.PByte = Byte.MinValue;
            nMin.PSByte = SByte.MinValue;
            nMin.PShort = Int16.MinValue;
            nMin.PUShort = UInt16.MinValue;
            nMin.PInteger = Int32.MinValue;
            nMin.PUInteger = UInt32.MinValue;
            nMin.PLong = Int64.MinValue;
            nMin.PULong = UInt64.MinValue;
            nMin.PFloat = Single.MinValue;
            nMin.PDouble = Double.MinValue;
            nMin.PDecimal = Decimal.MinValue;
            nMin.PBigInteger = BigInteger.Parse( "-12345678901234567890123456789012345678901234567890123456789012345678901234567890", System.Globalization.NumberFormatInfo.InvariantInfo );
            nMin.PDateTime = Util.UtcMinValue;
            nMin.PDateTimeOffset = DateTimeOffset.MinValue;
            nMin.PTimeSpan = TimeSpan.MinValue;
            nMin.PGuid = Guid.Empty;

            var options = new PocoJsonSerializerOptions { Mode = mode };

            var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, options, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(max) serialization: " + t ) );
            nMax2.Should().BeEquivalentTo( nMax );

            var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, options, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(min) serialization: " + t ) );
            nMin2.Should().BeEquivalentTo( nMin );
        }


    }
}
