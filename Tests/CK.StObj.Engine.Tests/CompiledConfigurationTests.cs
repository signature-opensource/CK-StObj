using CK.Core;
using CK.Testing;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class CompiledConfigurationTests
{
    public sealed class SomeObject : IRealObject
    {
    }

    [Test]
    public async Task the_burnt_values_and_the_locks_are_in_the_generated_map_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        var binPath = configuration.FirstBinPath;
        binPath.Types.Add( typeof( SomeObject ) );
        binPath.CompiledValues.Add( "Host:Options:Delay", "12" );
        binPath.CompiledValues.Add( "Absent", null );
        binPath.CompiledLocks.Add( "Parties:[*]:Address" );
        ICKomposableMap map = (await configuration.RunSuccessfullyAsync()).LoadMap();
        var compiled = map.CompiledConfiguration;
        compiled.Values.Count.ShouldBe( 2 );
        compiled.Values["host:options:delay"].ShouldBe( "12" );
        compiled.Values.ShouldContainKeyAndValue( "Absent", null );
        compiled.Locks.Select( l => l.Text ).ShouldBe( ["Parties:[*]:Address"] );
        compiled.IsLocked( "Parties:2:Address" ).ShouldBeTrue();
    }

    [Test]
    public async Task without_burnt_values_the_compiled_configuration_is_empty_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SomeObject ) );
        var map = (await configuration.RunSuccessfullyAsync()).LoadMap();
        map.CompiledConfiguration.ShouldBeSameAs( CompiledConfiguration.Empty );
    }
}
