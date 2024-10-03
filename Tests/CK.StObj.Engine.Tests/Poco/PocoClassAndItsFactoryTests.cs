using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
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
    public void poco_knows_its_Factory()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IPocoKnowsItsFactory ));
        using var auto = configuration.Run().CreateAutomaticServices();

        var f = auto.Services.GetRequiredService<IPocoFactory<IPocoKnowsItsFactory>>();
        var o = f.Create();
        var f2 = ((IPocoGeneratedClass)o).Factory;
        f.Should().BeSameAs( f2 );
    }

}
