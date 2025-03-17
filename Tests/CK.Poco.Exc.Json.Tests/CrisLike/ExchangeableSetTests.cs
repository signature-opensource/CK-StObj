using CK.Core;
using CK.CrisLike;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests.CrisLike;

/// <summary>
/// This record will never be serialized.
/// </summary>
/// <param name="SecureName">A secret that must not be serialized: it must remain in memory only.</param>
[NotSerializable]
public record struct MemoryOnlyData( string SecureName );

/// <summary>
/// This record will never be exchanged.
/// </summary>
/// <param name="LocalName">This name must not leave nor enter this party.</param>
[NotExchangeable]
public record struct LocalOnlyData( string LocalName );

public record struct Data( LocalOnlyData LocalOnly,
                           string Name,
                           int Power,
                           Guid Id,
                           double Ratio,
                           MemoryOnlyData MemoryOnly );

public interface IThing : IPoco
{
    ref Data Data { get; }

    DateTime CreationDate { get; set; }

    TimeSpan LifeTime { get; set; }

    NormalizedCultureInfo Culture { get; set; }

    ref MemoryOnlyData LocalOnly { get; }
}

public record struct MoreData( ExtendedCultureInfo CultureExt );

public interface IThingMore : IThing
{
    ref MoreData MoreData { get; }

    IList<Data> MultipleDatas { get; }

    byte[] Raw { get; set; }
}

public interface IDualThingCreateCommand : ICommandAuthDeviceId
{
    IThing T1 { get; set; }

    IThing? T2 { get; set; }
}

[TestFixture]
public class ExchangeableSetTests
{
    static IThingMore CreateThing( PocoDirectory d )
    {
        var t = d.Create<IThingMore>();
        t.CreationDate = new DateTime( 2024, 2, 12, 12, 6, 28 );
        t.LifeTime = new TimeSpan( 5, 30, 0 );
        t.Culture = NormalizedCultureInfo.EnsureNormalizedCultureInfo( "fr-CA" );
        t.Data.Name = "Albert";
        t.Data.Power = 1;
        t.Data.Id = Guid.Parse( "{5A246ADD-2E07-4255-B957-2193FE06DB42}" );
        t.Data.Ratio = Math.PI;
        t.MultipleDatas.Add( new Data( new LocalOnlyData( "LOCAL-DATA-ONLY" ), "Eva", 2, Guid.Empty, Math.E, new MemoryOnlyData( "NOWAY" ) ) );
        t.MoreData.CultureExt = ExtendedCultureInfo.EnsureExtendedCultureInfo( "fr,es,de,en" );
        t.Raw = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        return t;
    }

    [Test]
    public async Task not_serializable_and_not_exchangeable_attributes_are_handled_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IThingMore ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();
        var thing = CreateThing( directory );

        // Using ToString: the "AllSerializable" set is used.
        // Fields use the Pascal casing.
        PocoJsonExportOptions.ToStringDefault.TypeFilterName.ShouldBe( "AllSerializable" );
        string? t = thing.ToString();
        Throw.DebugAssert( t != null );
        t.ShouldContain( "Power", Case.Sensitive );
        t.ShouldNotContain( "power", Case.Sensitive );
        // The [NotSerializable] doesn't appear.
        t.ShouldNotContain( "SecureName" );
        t.ShouldNotContain( "NOWAY" );
        // But the [NotExchangeable] appears.
        t.ShouldContain( "LocalName" );
        t.ShouldContain( "LOCAL-DATA-ONLY" );


        // Using default export/import options: set is "AllExchangeable".
        // Fields use the camel casing.
        PocoJsonExportOptions.Default.TypeFilterName.ShouldBe( "AllExchangeable" );
        JsonTestHelper.Roundtrip( directory, thing, text: text => t = text );
        Throw.DebugAssert( t != null );
        t.ShouldContain( "power", Case.Sensitive );
        t.ShouldNotContain( "Power", Case.Sensitive );
        // Both [NotSerializable] and [NotExchangeable] don't appear.
        t.ShouldNotContain( "secureName" );
        t.ShouldNotContain( "NOWAY" );
        t.ShouldNotContain( "localName" );
        t.ShouldNotContain( "LOCAL-DATA-ONLY" );
    }

    public record struct SafeData( LocalOnlyData LocalOnly, MemoryOnlyData MemoryOnly );

    public interface ICannotBeSpoofed : IPoco
    {
        ref SafeData SafeData { get; }
        ref LocalOnlyData LocalOnly { get; }
        ref MemoryOnlyData MemoryOnly { get; }
        [DefaultValue( 3712 )]
        int IAmHere { get; set; }
    }

    [Test]
    public async Task PocoTypeSet_restrictions_cannot_be_spoofed_for_Poco_and_Record_fields_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( ICannotBeSpoofed ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();
        var poco = directory.Create<ICannotBeSpoofed>();
        poco.SafeData.MemoryOnly = new MemoryOnlyData( SecureName: "MEMORY-ONLY-1" );
        poco.SafeData.LocalOnly = new LocalOnlyData( LocalName: "LOCAL-ONLY-1" );
        poco.MemoryOnly.SecureName = "MEMORY-ONLY-2";
        poco.LocalOnly.LocalName = "LOCAL-ONLY-2";
        poco.IAmHere = 42;
        // [NotSerializable] cannot be serialized at all.
        poco.ToString().ShouldBe( """{"SafeData":{"LocalOnly":{"LocalName":"LOCAL-ONLY-1"}},"LocalOnly":{"LocalName":"LOCAL-ONLY-2"},"IAmHere":42}""" );

        // Forged payload.
        var attack = """{"SafeData":{"MemoryOnly":{"SecureName":"EVIL-1"},"LocalOnly":{"LocalName":"LOCAL-IN-1"}},"MemoryOnly":{"SecureName":"EVIL-2"},"LocalOnly":{"LocalName":"LOCAL-IN-2"},"IAmHere":-5}""";

        // Reading with "AllSerializable": [NotSerializable] types are ignored.
        var factory = directory.Find<ICannotBeSpoofed>();
        Throw.DebugAssert( factory != null );
        var r1 = factory.ReadJson( attack, new PocoJsonImportOptions { TypeFilterName = "AllSerializable" } )!;
        r1.SafeData.MemoryOnly.SecureName.ShouldBe( "" );
        r1.SafeData.LocalOnly.LocalName.ShouldBe( "LOCAL-IN-1" );
        r1.MemoryOnly.SecureName.ShouldBe( "" );
        r1.LocalOnly.LocalName.ShouldBe( "LOCAL-IN-2" );
        r1.IAmHere.ShouldBe( -5 );

        // Reading with "AllExchangeable": [NotExchangeable] types are also ignored.
        var r2 = factory.ReadJson( attack, new PocoJsonImportOptions { TypeFilterName = "AllExchangeable" } )!;
        r2.SafeData.MemoryOnly.SecureName.ShouldBe( "" );
        r2.SafeData.LocalOnly.LocalName.ShouldBe( "" );
        r2.MemoryOnly.SecureName.ShouldBe( "" );
        r2.LocalOnly.LocalName.ShouldBe( "" );
        r2.IAmHere.ShouldBe( -5 );
    }

    public interface ICannotBeSpoofed2 : IPoco
    {
        ref (MemoryOnlyData, LocalOnlyData Local) SafeData { get; }
        IList<(MemoryOnlyData Memory, LocalOnlyData)> More { get; }
    }

    [Test]
    public async Task PocoTypeSet_restrictions_cannot_be_spoofed_for_anonymous_records_fields_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( ICannotBeSpoofed2 ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();
        var poco = directory.Create<ICannotBeSpoofed2>();
        poco.SafeData.Item1 = new MemoryOnlyData( SecureName: "MEMORY-ONLY-1" );
        poco.SafeData.Local = new LocalOnlyData( LocalName: "LOCAL-ONLY-1" );
        poco.More.Add( (new MemoryOnlyData( SecureName: "MEMORY-ONLY-2" ), new LocalOnlyData( LocalName: "LOCAL-ONLY-2" )) );

        // [NotSerializable] cannot be serialized at all.
        // Serialization use array syntax for value tuples.
        poco.ToString().ShouldBe( """{"SafeData":[{"LocalName":"LOCAL-ONLY-1"}],"More":[[{"LocalName":"LOCAL-ONLY-2"}]]}""" );

        var factory = directory.Find<ICannotBeSpoofed2>();
        Throw.DebugAssert( factory != null );

        // Forged payload with array syntax: the number of cells are incorrect, reading this throws.
        var attack1 = """{"SafeData":[{"SecureName":"EVIL-1"},{"LocalName":"LOCAL-IN-1"}],"More":[[{"SecureName":"EVIL-1"},{"LocalName":"LOCAL-IN-2"}]]}""";

        Util.Invokable( () => factory.ReadJson( attack1, new PocoJsonImportOptions { TypeFilterName = "AllSerializable" } ) )
                     .ShouldThrow<JsonException>();

        // Using the object syntax that is allowed by anonymous records:
        var attack2 = """{"SafeData":{"Item1":{"SecureName":"EVIL-1"},"Item2":{"LocalName":"LOCAL-IN-1"}},"More":[{"Memory":{"SecureName":"EVIL-1"},"Item2":{"LocalName":"LOCAL-IN-2"}}]}""";
        // Reading with "AllSerializable": [NotSerializable] types are ignored.
        var r1 = factory.ReadJson( attack2, new PocoJsonImportOptions { TypeFilterName = "AllSerializable" } )!;
        r1.SafeData.Item1.SecureName.ShouldBe( "" );
        r1.SafeData.Local.LocalName.ShouldBe( "LOCAL-IN-1" );
        r1.More.Count.ShouldBe( 1 );
        r1.More[0].Memory.SecureName.ShouldBe( "" );
        r1.More[0].Item2.LocalName.ShouldBe( "LOCAL-IN-2" );

        // Reading with "AllExchangeable": [NotExchangeable] types are also ignored.
        var r2 = factory.ReadJson( attack2, new PocoJsonImportOptions { TypeFilterName = "AllExchangeable" } )!;
        r2.SafeData.Item1.SecureName.ShouldBe( "" );
        r2.SafeData.Local.LocalName.ShouldBe( "" );
        r2.More.Count.ShouldBe( 0, "The List itself is excluded from the type set!" );
    }
}
