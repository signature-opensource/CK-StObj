using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;


[TestFixture]
public partial class BasicTypeTests
{
    [ExternalName( "BasicTypes" )]
    public interface IAllBasicTypes : IPoco
    {
        bool PBool { get; set; }
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
        char PChar { get; set; }
    }

    [Test]
    public async Task all_basic_types_roundtrip_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IAllBasicTypes ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var nMax = auto.Services.GetRequiredService<IPocoFactory<IAllBasicTypes>>().Create();
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
        nMax.PChar = char.MaxValue;

        var nMin = auto.Services.GetRequiredService<IPocoFactory<IAllBasicTypes>>().Create();
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
        nMin.PChar = char.MinValue;

        var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(max) serialization: " + t ) );
        nMax2.ShouldBeEquivalentTo( nMax );

        var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(min) serialization: " + t ) );
        nMin2.ShouldBeEquivalentTo( nMin );
    }

    [ExternalName( "NullableBasicTypes" )]
    public interface IAllNullableBasicTypes : IPoco
    {
        byte? PByte { get; set; }
        sbyte? PSByte { get; set; }
        short? PShort { get; set; }
        ushort? PUShort { get; set; }
        int? PInteger { get; set; }
        uint? PUInteger { get; set; }
        long? PLong { get; set; }
        ulong? PULong { get; set; }
        float? PFloat { get; set; }
        double? PDouble { get; set; }
        decimal? PDecimal { get; set; }
        BigInteger? PBigInteger { get; set; }
        DateTime? PDateTime { get; set; }
        DateTimeOffset? PDateTimeOffset { get; set; }
        TimeSpan? PTimeSpan { get; set; }
        Guid? PGuid { get; set; }
        char? PChar { get; set; }
    }

    [Test]
    public async Task all_nullable_basic_types_roundtrip_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IAllNullableBasicTypes ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var nNull = auto.Services.GetRequiredService<IPocoFactory<IAllNullableBasicTypes>>().Create();

        var nMax = auto.Services.GetRequiredService<IPocoFactory<IAllNullableBasicTypes>>().Create();
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
        nMax.PChar = char.MaxValue;

        var nMin = auto.Services.GetRequiredService<IPocoFactory<IAllNullableBasicTypes>>().Create();
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
        nMin.PChar = char.MinValue;

        var nNull2 = JsonTestHelper.Roundtrip( directory, nNull, text: t => TestHelper.Monitor.Info( $"IAllNullableBasicTypes (null) serialization: " + t ) );
        nNull2.ShouldBeEquivalentTo( nNull );

        var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, text: t => TestHelper.Monitor.Info( $"IAllNullableBasicTypes (max) serialization: " + t ) );
        nMax2.ShouldBeEquivalentTo( nMax );

        var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text: t => TestHelper.Monitor.Info( $"IAllNullableBasicTypes (min) serialization: " + t ) );
        nMin2.ShouldBeEquivalentTo( nMin );
    }

    public interface IWithNonNullables : IPoco
    {
        string Name { get; set; }
        int Count { get; set; }
        IList<string> Names { get; }
    }

    [TestCase( "Name" )]
    [TestCase( "Count" )]
    [TestCase( "Names" )]
    public async Task null_for_a_non_nullable_field_is_a_JsonException_Async( string field )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithNonNullables ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var factory = auto.Services.GetRequiredService<IPocoFactory<IWithNonNullables>>();

        factory.ReadJson( """{"Name":"n","Count":1,"Names":["a"]}""" )!.Name.ShouldBe( "n" );
        var json = $$"""{"{{field}}":null}""";
        Util.Invokable( () => factory.ReadJson( json ) ).ShouldThrow<System.Text.Json.JsonException>();
        // A null item in a list of non nullable strings too.
        Util.Invokable( () => factory.ReadJson( """{"Names":["a",null]}""" ) ).ShouldThrow<System.Text.Json.JsonException>();
    }

    public enum ByteCode : byte { Zero, One, Two }

    public interface IWithIntegers : IPoco
    {
        int Count { get; set; }
        byte Small { get; set; }
        ulong Big { get; set; }
        TimeSpan Delay { get; set; }
        ByteCode Code { get; set; }
    }

    [Test]
    public async Task the_integral_types_accept_any_integral_JSON_number_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithIntegers ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var factory = auto.Services.GetRequiredService<IPocoFactory<IWithIntegers>>();

        // JSON has a single number type: 1, 1.0 and 1e0 are the same value (a JSON Schema "integer" accepts them all).
        var o = factory.ReadJson( """{"Count":3712.0,"Small":2.55e2,"Big":1e19,"Delay":10.000,"Code":2.0}""" )!;
        o.Count.ShouldBe( 3712 );
        o.Small.ShouldBe( (byte)255 );
        o.Big.ShouldBe( 10_000_000_000_000_000_000UL );
        o.Delay.ShouldBe( TimeSpan.FromTicks( 10 ) );
        o.Code.ShouldBe( ByteCode.Two );
    }

    [TestCase( """{"Count":1.5}""" )]
    [TestCase( """{"Count":2147483648}""" )]
    [TestCase( """{"Small":256}""" )]
    [TestCase( """{"Small":-1.0}""" )]
    [TestCase( """{"Big":-1}""" )]
    [TestCase( """{"Big":1e20}""" )]
    [TestCase( """{"Code":0.5}""" )]
    public async Task a_non_integral_or_out_of_range_number_is_a_JsonException_Async( string json )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithIntegers ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var factory = auto.Services.GetRequiredService<IPocoFactory<IWithIntegers>>();

        Util.Invokable( () => factory.ReadJson( json ) ).ShouldThrow<System.Text.Json.JsonException>();
    }
}
