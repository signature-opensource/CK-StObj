using CK.Core;
using CK.Setup;
using CK.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class PocoClassAndItsFactoryTests
{

    public interface IPocoKnowsItsFactory : IPoco
    {
        int One { get; set; }
    }

    [Test]
    public async Task poco_knows_its_Factory_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IPocoKnowsItsFactory ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var f = auto.Services.GetRequiredService<IPocoFactory<IPocoKnowsItsFactory>>();
        var o = f.Create();
        var f2 = ((IPocoGeneratedClass)o).Factory;
        f.ShouldBeSameAs( f2 );
    }

}
