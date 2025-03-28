using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;


[TestFixture]
public class EnumSupportTest
{
    [ExternalName( "WorkingCode" )]
    public enum Code
    {
        None,
        Working,
        Pending
    }

    public interface ITest : IPoco
    {
        Code Working { get; set; }

        Code? NullableWorking { get; set; }

        object? Result { get; set; }
    }

    [Test]
    public async Task enum_serialization_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( ITest ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var f = auto.Services.GetRequiredService<IPocoFactory<ITest>>();
        var o = f.Create( o => { o.Working = Code.Pending; o.NullableWorking = Code.Working; o.Result = Code.None; } );
        o.ToString().ShouldBe( @"{""Working"":2,""NullableWorking"":1,""Result"":[""WorkingCode"",0]}" );
    }

    [Test]
    public async Task enum_serialization_roundtrip_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( ITest ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var f = auto.Services.GetRequiredService<IPocoFactory<ITest>>();
        var o = f.Create( o => { o.Working = Code.Pending; o.NullableWorking = Code.Working; o.Result = Code.None; } );
        var o2 = JsonTestHelper.Roundtrip( directory, o );

        Debug.Assert( o2 != null );
        o2.Working.ShouldBe( Code.Pending );
        o2.NullableWorking.ShouldBe( Code.Working );
        o2.Result.ShouldBe( Code.None );

        o.NullableWorking = null;
        o2 = JsonTestHelper.Roundtrip( directory, o );
        o2.Working.ShouldBe( Code.Pending );
        o2.NullableWorking.ShouldBeNull();
        o2.Result.ShouldBe( Code.None );
    }

    [ExternalName( "ULCode" )]
    public enum CodeOnULong : ulong
    {
        None,
        Working = long.MaxValue,
        Pending = ulong.MaxValue
    }

    public interface IWithULong : IPoco
    {
        CodeOnULong Code { get; set; }

        object? Result { get; set; }
    }

    [Test]
    public async Task enum_serialization_with_ulong_underlying_type_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithULong ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var f = auto.Services.GetRequiredService<IPocoFactory<IWithULong>>();
        var o = f.Create( o => { o.Code = CodeOnULong.None; o.Result = CodeOnULong.None; } );
        o.ToString().ShouldBe( @"{""Code"":""0"",""Result"":[""ULCode"",""0""]}" );

        o.Code = CodeOnULong.Working;
        o.Result = o.Code;
        o.ToString().ShouldBe( @"{""Code"":""9223372036854775807"",""Result"":[""ULCode"",""9223372036854775807""]}" );

        o.Code = CodeOnULong.Pending;
        o.Result = o.Code;
        o.ToString().ShouldBe( @"{""Code"":""18446744073709551615"",""Result"":[""ULCode"",""18446744073709551615""]}" );
    }

    public interface IWithList : IPoco
    {
        IList<Code> Codes { get; }
    }

    [Test]
    public async Task enum_serialization_in_list_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithList ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var f = auto.Services.GetRequiredService<IPocoFactory<IWithList>>();
        var o = f.Create( o =>
        {
            o.Codes.Add( Code.Working );
            o.Codes.Add( Code.Pending );
        } );
        o.ToString().ShouldBe( """{"Codes":[1,2]}""" );

        JsonTestHelper.Roundtrip( directory, o );
    }

}
