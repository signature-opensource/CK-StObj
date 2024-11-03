using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests;

[TestFixture]
public class UnionTypeSupportTest
{
    public interface IBasicUnion : IPoco
    {
        [UnionType]
        object Thing { get; set; }

        class UnionTypes
        {
            public (string, int, decimal) Thing { get; }
        }
    }


    [Test]
    public async Task union_serialization_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.AddRangeArray( typeof( CommonPocoJsonSupport ), typeof( IBasicUnion ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var o = directory.Create<IBasicUnion>();
        o.ToString().Should().Be( @"{""Thing"":[""string"",""""]}", "The first possible default is selected, here it's the string that defaults to empty." );

        o.Thing = "Hip!";
        o.ToString().Should().Be( @"{""Thing"":[""string"",""Hip!""]}" );

        o.Thing = 3712;
        o.ToString().Should().Be( @"{""Thing"":[""int"",3712]}" );

        o.Thing = 3712m;
        o.ToString().Should().Be( @"{""Thing"":[""decimal"",""3712""]}" );
    }

}
