using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

[TestFixture]
public class SingletonAttributeTests
{
    [SingletonService]
    public class NotConstructibleService
    {
        private NotConstructibleService() { }

        public static NotConstructibleService Create() => new NotConstructibleService();
    }

    public class NotConstructibleServiceNaked
    {
        private NotConstructibleServiceNaked() { }

        public static NotConstructibleServiceNaked Create() => new NotConstructibleServiceNaked();
    }

    public class SomeSingletonFailed : ISingletonAutoService
    {
        public SomeSingletonFailed( NotConstructibleServiceNaked c )
        {
        }
    }

    [Test]
    public void without_SingletonServiceAttribute_scope_lifetime_fails()
    {
        TestHelper.GetFailedCollectorResult( [typeof( SomeSingletonFailed ), typeof( NotConstructibleServiceNaked )],
            "is marked as IsSingleton but parameter 'c' of type 'NotConstructibleServiceNaked' in constructor is Scoped" );
    }

    public class SomeScoped : IScopedAutoService
    {
        public SomeScoped( NotConstructibleServiceNaked c )
        {
        }
    }

    [Test]
    public async Task without_SingletonServiceAttribute_scope_requires_manual_registration_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SomeScoped ), typeof( NotConstructibleServiceNaked ) );
        var map = (await configuration.RunSuccessfullyAsync().ConfigureAwait( false )).FirstBinPath.LoadMap();

        using var auto1 = map.CreateAutomaticServices();
        auto1.Services.Invoking( s => s.GetService<SomeScoped>() ).Should().Throw<InvalidOperationException>();

        using var auto2 = map.CreateAutomaticServices( configureServices: services => services.AddScoped( sp => NotConstructibleServiceNaked.Create() ) );
        auto2.Services.GetService<SomeScoped>().Should().NotBeNull();
    }

    public class SomeSingleton : ISingletonAutoService
    {
        public SomeSingleton( NotConstructibleService c )
        {
        }
    }


    [Test]
    public async Task SingletonServiceAttribute_enables_services_to_not_have_public_constructor_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( SomeSingleton ), typeof( NotConstructibleService ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: services =>
        {
            services.AddSingleton( sp => NotConstructibleService.Create() );
        } );
        auto.Services.GetService<SomeSingleton>().Should().NotBeNull();
    }

}
