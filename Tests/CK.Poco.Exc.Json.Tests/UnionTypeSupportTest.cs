using CK.Core;
using CK.Testing;
using Shouldly;
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
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IBasicUnion ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var o = directory.Create<IBasicUnion>();
        o.ToString().ShouldBe( @"{""Thing"":[""string"",""""]}", "The first possible default is selected, here it's the string that defaults to empty." );

        o.Thing = "Hip!";
        o.ToString().ShouldBe( @"{""Thing"":[""string"",""Hip!""]}" );

        o.Thing = 3712;
        o.ToString().ShouldBe( @"{""Thing"":[""int"",3712]}" );

        o.Thing = 3712m;
        o.ToString().ShouldBe( @"{""Thing"":[""decimal"",""3712""]}" );
    }

}
