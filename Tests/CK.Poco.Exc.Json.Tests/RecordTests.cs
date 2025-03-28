using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;

[TestFixture]
public class RecordTests
{
    public record struct Thing( string Name, int Count );

    public interface IWithRecord : IPoco
    {
        ref Thing Hop { get; }
    }

    [Test]
    public async Task simple_tuple_serialization_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithRecord ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var f = auto.Services.GetRequiredService<IPocoFactory<IWithRecord>>();
        var o = f.Create( o =>
        {
            o.Hop.Name = "Albert";
            o.Hop.Count = 3712;
        } );
        o.ToString().ShouldBe( @"{""Hop"":{""Name"":""Albert"",""Count"":3712}}" );

        var o2 = JsonTestHelper.Roundtrip( directory, o );
        o2.Hop.ShouldBe( new Thing( "Albert", 3712 ) );
    }

    public interface IWithNullableRecord : IPoco
    {
        ref Thing? Hop { get; }
    }

    [Test]
    public async Task simple_nullable_tuple_serialization_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithNullableRecord ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var f = auto.Services.GetRequiredService<IPocoFactory<IWithNullableRecord>>();
        var o = f.Create( o => { o.Hop = new Thing( "Hip", 42 ); } );
        o.ToString().ShouldBe( @"{""Hop"":{""Name"":""Hip"",""Count"":42}}" );

        var o2 = JsonTestHelper.Roundtrip( directory, o );
        o2.Hop.ShouldBe( new Thing( "Hip", 42 ) );

        o.Hop = null;
        o.ToString().ShouldBe( @"{""Hop"":null}" );

        var o3 = JsonTestHelper.Roundtrip( directory, o );
        o3.Hop.ShouldBeNull();
    }

}
