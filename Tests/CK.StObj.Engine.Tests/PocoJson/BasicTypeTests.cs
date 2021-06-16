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
            IList<object> PObjects { get; }
        }

        [TestCase( PocoSerializerMode.Server )]
        [TestCase( PocoSerializerMode.ECMAScriptSafe )]
        public void all_basic_types_roundtrip_in_server_and_ECMAScriptSafeMode( PocoSerializerMode mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllBasicTypes ) ); ;
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();

            IAllBasicTypes nMax = CreateMax( services );

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
            nMin.PObjects.AddRangeArray( nMin.PByte,
                                         nMin.PSByte,
                                         nMin.PShort,
                                         nMin.PUShort,
                                         nMin.PInteger,
                                         nMin.PUInteger,
                                         nMin.PLong,
                                         nMin.PULong,
                                         nMin.PFloat,
                                         nMin.PDouble,
                                         nMin.PDecimal,
                                         nMin.PBigInteger,
                                         nMin.PDateTime,
                                         nMin.PDateTimeOffset,
                                         nMin.PTimeSpan,
                                         nMin.PGuid );

            var options = new PocoJsonSerializerOptions { Mode = mode };

            var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, options, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(max) serialization: " + t ) );
            nMax2.Should().BeEquivalentTo( nMax );

            var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, options, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(min) serialization: " + t ) );
            nMin2.Should().BeEquivalentTo( nMin );
        }

        static IAllBasicTypes CreateMax( ServiceProvider s )
        {
            var nMax = s.GetService<IPocoFactory<IAllBasicTypes>>().Create();
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
            nMax.PObjects.AddRangeArray( nMax.PByte,
                                         nMax.PSByte,
                                         nMax.PShort,
                                         nMax.PUShort,
                                         nMax.PInteger,
                                         nMax.PUInteger,
                                         nMax.PLong,
                                         nMax.PULong,
                                         nMax.PFloat,
                                         nMax.PDouble,
                                         nMax.PDecimal,
                                         nMax.PBigInteger,
                                         nMax.PDateTime,
                                         nMax.PDateTimeOffset,
                                         nMax.PTimeSpan,
                                         nMax.PGuid );
            return nMax;
        }

        [Test]
        public void types_roundtrip_with_max_values_in_ECMAScriptStandard_mode_except_for_floats_that_are_always_read_as_doubles()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IAllBasicTypes ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();
            IAllBasicTypes nMax = CreateMax( services );

            // We avoid casting floats that are in the range [float.MinValue, float.MaxValue] because
            // this would lose the 53 bits precision of the double. (Typical use of doubles is to normalize
            // them in [-1.0,1.0].)
            nMax.PObjects[nMax.PObjects.IndexOf( o => o is float )] = 0.1;

            var options = new PocoJsonSerializerOptions { Mode = PocoSerializerMode.ECMAScriptStandard };

            var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, options, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(max) ECMAScriptStandard serialization: " + t ) );
            nMax2.Should().BeEquivalentTo( nMax );
        }

        [TestCase( 0.0, "b,sb,s,us,i,ui,f" )]
        [TestCase( 1.0, "b,sb,s,us,i,ui,f" )]
        [TestCase( 127.0, "b,sb,s,us,i,ui,f" )]
        [TestCase( 128, "b,s,us,i,ui,f" )]
        [TestCase( 255, "b,s,us,i,ui,f" )]
        [TestCase( 256, "s,us,i,ui,f" )]
        [TestCase( 32767, "s,us,i,ui,f" )]
        [TestCase( 32768, "us,i,ui,f" )]
        [TestCase( 65535, "us,i,ui,f" )]
        [TestCase( 65536, "i,ui,f" )]
        [TestCase( Int32.MaxValue, "i,ui,f" )]
        [TestCase( UInt32.MaxValue, "ui,f" )]
        [TestCase( UInt32.MaxValue + 1.0, "f" )]

        [TestCase( -1.0, "sb,s,i,f" )]
        [TestCase( -128.0, "sb,s,i,f" )]
        [TestCase( -129.0, "s,i,f" )]
        [TestCase( -32768, "s,i,f" )]
        [TestCase( -32769, "i,f" )]
        [TestCase( Int32.MinValue, "i,f" )]
        [TestCase( Int32.MinValue - 1.0, "f" )]

        // This works and this should not!
        [TestCase( 1.5, "b,sb,s,us,i,ui,f" )]
        public void Standard_Convert_methods_from_double_work_as_expected_except_for_fractionals( double number, string ok )
        {
            ok = "," + ok + ",";
            void Check<T>( string t, Func<double,T> convert )
            {
                if( ok.Contains( "," + t + ",", StringComparison.Ordinal ) )
                {
                    T v = convert( number );
                }
                else
                {
                    FluentActions.Invoking( () => convert( number ) ).Should().Throw<OverflowException>();
                }
            }
            Check( "b", Convert.ToByte );
            Check( "sb", Convert.ToSByte );
            Check( "s", Convert.ToInt16 );
            Check( "us", Convert.ToUInt16 );
            Check( "i", Convert.ToInt32 );
            Check( "ui", Convert.ToUInt32 );
            Check( "f", Convert.ToSingle );
        }

    }
}
