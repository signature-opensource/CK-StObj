using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

public class SetAutoServiceKindTests
{
    public interface IService
    {
    }

    public class TheService : IService, IAutoService
    {
    }

    [Test]
    public async Task simple_front_only_registration_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IService ), ConfigurableAutoServiceKind.IsScoped | ConfigurableAutoServiceKind.IsMultipleService )
                                 .Add( typeof( TheService ) );

        var map = (await configuration.RunAsync().ConfigureAwait(false)).LoadMap();

        var d = map.Services.Mappings[typeof( TheService )];
        d.AutoServiceKind.ShouldBe( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
        d.MultipleMappings.ShouldContain( typeof( IService ) );
    }

    // This is defined as a Singleton:
    // SetAutoServiceKind( "...+OpenGeneric`1, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, true );
    public class OpenGeneric<T> where T : struct { public T MagicValue; }

    public class GenService : ISingletonAutoService
    {
        public GenService( OpenGeneric<int> dep )
        {
        }
    }

    [Test]
    public void late_resolving_open_generics()
    {
        var collector = new Setup.StObjCollector();
        collector.SetAutoServiceKind( TestHelper.Monitor,
                                      "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+OpenGeneric`1, CK.StObj.Engine.Tests",
                                      ConfigurableAutoServiceKind.IsSingleton,
                                      isOptional: true );
        collector.RegisterType( TestHelper.Monitor, typeof( GenService ) );
        var r = collector.GetResult( TestHelper.Monitor );
        r.HasFatalError.ShouldBeFalse();
    }


    public interface IConfiguration { }

    public interface IConfigurationSection : IConfiguration { }

    public class ThisIsTheConfig : IConfiguration { }

    public class ThisShouldCoexist1 : IConfigurationSection { }
    public class ThisShouldCoexist2 : IConfigurationSection { }

    [Test]
    public async Task base_singleton_interface_definition_can_coexist_with_specializations_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IConfiguration ), ConfigurableAutoServiceKind.IsSingleton )
                                 .Add( typeof( ThisShouldCoexist1 ), typeof( ThisIsTheConfig ), typeof( ThisShouldCoexist2 ) );

        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: register =>
        {
            // This is done by .net configuration extension.
            register.AddSingleton<IConfiguration>( new ThisIsTheConfig() );
        } );

        auto.Services.GetService<IConfiguration>( throwOnNull: true ).ShouldBeOfType<ThisIsTheConfig>();
        auto.Services.GetService<IConfigurationSection>( throwOnNull: false ).ShouldBeNull();
    }

}
