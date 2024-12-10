using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Numerics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;

[TestFixture]
public partial class GlobalizationTypeTests
{
    [ExternalName( "GlobalizationTypes" )]
    public interface IAllTypes : IPoco
    {
        FormattedString? PFormattedString { get; set; }
        CodeString PCodeString { get; set; }
        MCString PMCString { get; set; }
        UserMessage? PUserMessage { get; set; }
        SimpleUserMessage? PSimpleUserMessage { get; set; }
        NormalizedCultureInfo PNormalizedCultureInfo { get; set; }
        ExtendedCultureInfo PExtendedCultureInfo { get; set; }
    }

    [Test]
    public async Task all_globalization_types_roundtrip_Async()
    {
        var current = new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.CodeDefault );

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IAllTypes ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var n = auto.Services.GetRequiredService<IPocoFactory<IAllTypes>>().Create();
        n.PFormattedString = FormattedString.Create( $"Hello {nameof( current )}!", NormalizedCultureInfo.CodeDefault );
        n.PCodeString = CodeString.Create( $"Hello {nameof( current )}!", NormalizedCultureInfo.CodeDefault, "Res.Hello", "file", 3712 );
        n.PMCString = MCString.Create( current, $"Hello {nameof( current )}!", "Res.Hello" );
        n.PUserMessage = UserMessage.Info( current, "Hop!", "Res.Hop" );
        n.PSimpleUserMessage = new SimpleUserMessage( UserMessageLevel.Error, "Error", 5 );
        n.PNormalizedCultureInfo = NormalizedCultureInfo.CodeDefault;
        n.PExtendedCultureInfo = ExtendedCultureInfo.EnsureExtendedCultureInfo("fr, es" );

        var n2 = JsonTestHelper.Roundtrip( directory, n, text: t => TestHelper.Monitor.Info( $"IAllTypes serialization: " + t ) );
        n2.Should().BeEquivalentTo( n );
    }
}


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
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
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
        nMax2.Should().BeEquivalentTo( nMax );

        var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text: t => TestHelper.Monitor.Info( $"IAllBasicTypes(min) serialization: " + t ) );
        nMin2.Should().BeEquivalentTo( nMin );
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
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

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
        nNull2.Should().BeEquivalentTo( nNull );

        var nMax2 = JsonTestHelper.Roundtrip( directory, nMax, text: t => TestHelper.Monitor.Info( $"IAllNullableBasicTypes (max) serialization: " + t ) );
        nMax2.Should().BeEquivalentTo( nMax );

        var nMin2 = JsonTestHelper.Roundtrip( directory, nMin, text: t => TestHelper.Monitor.Info( $"IAllNullableBasicTypes (min) serialization: " + t ) );
        nMin2.Should().BeEquivalentTo( nMin );
    }
}
