using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint;

[TestFixture]
public class MultipleMappingsEndpointTests
{
    [IsMultiple]
    public interface IMany { }

    public class ManyScoped : IMany, IScopedAutoService { }
    public class ManySingleton : IMany, ISingletonAutoService { }

    // Will be singleton.
    public class ManyAuto : IMany, IAutoService { }

    // Will be scoped when registered by the StObjCollector
    // Can be anything when registered in the global or an endpoint.
    public class ManyNothing : IMany { }

    public class ManyScoped2 : IMany, IScopedAutoService { }
    public class ManySingleton2 : IMany, ISingletonAutoService { }
    public class ManyAuto2 : IMany, IAutoService { }

    public class ManyConsumer : IAutoService
    {
        public ManyConsumer( IEnumerable<IMany> all )
        {
            All = all;
        }
        public IEnumerable<IMany> All { get; }
    }

    [DIContainerDefinition( DIContainerKind.Endpoint )]
    public abstract class FirstDIContainerDefinition : DIContainerDefinition<FirstDIContainerDefinition.Data>
    {
        public sealed class Data : IScopedData
        {
        }

        public override void ConfigureContainerServices( IServiceCollection services,
                                                        Func<IServiceProvider, Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped<IActivityMonitor, ActivityMonitor>();
            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
        }
    }

    [DIContainerDefinition( DIContainerKind.Endpoint )]
    public abstract class SecondDIContainerDefinition : DIContainerDefinition<SecondDIContainerDefinition.Data>
    {
        public sealed class Data : IScopedData
        {
        }

        public override void ConfigureContainerServices( IServiceCollection services,
                                                        Func<IServiceProvider, Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped<IActivityMonitor, ActivityMonitor>();
            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
        }
    }

    [Test]
    public void single_singleton()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManyAuto ),
                                        typeof( ManyConsumer ),
                                        typeof( FirstDIContainerDefinition ),
                                        typeof( SecondDIContainerDefinition ));
        using var auto = configuration.Run().CreateAutomaticServices();

        auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

        var g = auto.Services;
        var e1 = g.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<FirstDIContainerDefinition.Data>>().Single();
        var e2 = g.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<SecondDIContainerDefinition.Data>>().Single();
        using var s1 = e1.GetContainer().CreateScope();
        using var s2 = e2.GetContainer().CreateScope();

        var mG = g.GetRequiredService<ManyConsumer>();
        mG.All.Should().BeEquivalentTo( new IMany[] { g.GetRequiredService<ManyAuto>() } );

        var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
        m1.All.Should().BeEquivalentTo( mG.All );

        var m2 = s2.ServiceProvider.GetRequiredService<ManyConsumer>();
        m2.All.Should().BeEquivalentTo( mG.All );
    }

    [Test]
    public void multiple_singletons()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManyAuto ),
                                              typeof( ManySingleton ),
                                              typeof( ManyAuto2 ),
                                              typeof( ManySingleton2 ),
                                              typeof( ManyConsumer ),
                                              typeof( FirstDIContainerDefinition ),
                                              typeof( SecondDIContainerDefinition ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

        var g = auto.Services;
        var e1 = g.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<FirstDIContainerDefinition.Data>>().Single();
        var e2 = g.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<SecondDIContainerDefinition.Data>>().Single();
        using var s1 = e1.GetContainer().CreateScope();
        using var s2 = e2.GetContainer().CreateScope();

        var mG = g.GetRequiredService<ManyConsumer>();
        mG.All.Should().BeEquivalentTo( new IMany[] { g.GetRequiredService<ManyAuto>(),
                                                      g.GetRequiredService<ManyAuto2>(),
                                                      g.GetRequiredService<ManySingleton>(),
                                                      g.GetRequiredService<ManySingleton2>() } );

        var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
        m1.All.Should().BeEquivalentTo( mG.All );

        var m2 = s2.ServiceProvider.GetRequiredService<ManyConsumer>();
        m2.All.Should().BeEquivalentTo( mG.All );
    }

    [Test]
    public void single_scoped()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManyScoped ),
                                              typeof( ManyConsumer ),
                                              typeof( FirstDIContainerDefinition ),
                                              typeof( SecondDIContainerDefinition ));
        using var auto = configuration.Run().CreateAutomaticServices();

        auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

        using var g = auto.Services.CreateScope();
        var e1 = g.ServiceProvider.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<FirstDIContainerDefinition.Data>>().Single();
        var e2 = g.ServiceProvider.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<SecondDIContainerDefinition.Data>>().Single();
        using var s1 = e1.GetContainer().CreateScope();
        using var s2 = e2.GetContainer().CreateScope();

        var mG = g.ServiceProvider.GetRequiredService<ManyConsumer>();
        var gScoped = g.ServiceProvider.GetRequiredService<ManyScoped>();
        mG.All.Should().BeEquivalentTo( new IMany[] { gScoped } );

        var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
        var m1Scoped = s1.ServiceProvider.GetRequiredService<ManyScoped>();
        m1Scoped.Should().NotBeSameAs( gScoped );
        m1.All.Should().BeEquivalentTo( new IMany[] { m1Scoped } );

        var m2 = s2.ServiceProvider.GetRequiredService<ManyConsumer>();
        var m2Scoped = s2.ServiceProvider.GetRequiredService<ManyScoped>();
        m2Scoped.Should().NotBeSameAs( gScoped ).And.NotBeSameAs( m1Scoped );
        m2.All.Should().BeEquivalentTo( new IMany[] { m2Scoped } );
    }

    [Test]
    public void global_can_register_multiple_services()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManyScoped ),
                                        typeof( ManyScoped2 ),
                                        typeof( ManyConsumer ),
                                        typeof( FirstDIContainerDefinition ),
                                        typeof( SecondDIContainerDefinition ));
        using var auto = configuration.Run().CreateAutomaticServices( configureServices: s =>
        {
            s.AddScoped<ManyNothing>();
            s.AddScoped<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
        } );

        auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

        using var g = auto.Services.CreateScope();
        var e1 = g.ServiceProvider.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<FirstDIContainerDefinition.Data>>().Single();
        var e2 = g.ServiceProvider.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<SecondDIContainerDefinition.Data>>().Single();
        using var s1 = e1.GetContainer().CreateScope();
        using var s2 = e2.GetContainer().CreateScope();

        var mG = g.ServiceProvider.GetRequiredService<ManyConsumer>();
        var gScoped = g.ServiceProvider.GetRequiredService<ManyScoped>();
        var gScoped1 = g.ServiceProvider.GetRequiredService<ManyScoped2>();
        var gScoped2 = g.ServiceProvider.GetRequiredService<ManyNothing>();
        mG.All.Should().Contain( new IMany[] { gScoped, gScoped1, gScoped2 } );

        var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
        var m1Scoped = s1.ServiceProvider.GetRequiredService<ManyScoped>();
        var m1Scoped1 = s1.ServiceProvider.GetRequiredService<ManyScoped2>();
        var m1Scoped2 = s1.ServiceProvider.GetRequiredService<ManyNothing>();
        m1Scoped.Should().NotBeSameAs( gScoped );
        m1Scoped1.Should().NotBeSameAs( gScoped1 );
        m1Scoped2.Should().NotBeSameAs( gScoped2 );
        m1.All.Should().Contain( new IMany[] { m1Scoped, m1Scoped1, m1Scoped2 } );

        var m2 = s2.ServiceProvider.GetRequiredService<ManyConsumer>();
        var m2Scoped = s2.ServiceProvider.GetRequiredService<ManyScoped>();
        var m2Scoped1 = s2.ServiceProvider.GetRequiredService<ManyScoped2>();
        var m2Scoped2 = s2.ServiceProvider.GetRequiredService<ManyNothing>();
        m2Scoped.Should().NotBeSameAs( gScoped ).And.NotBeSameAs( m1Scoped );
        m2Scoped1.Should().NotBeSameAs( gScoped1 ).And.NotBeSameAs( m1Scoped1 );
        m2Scoped2.Should().NotBeSameAs( gScoped2 ).And.NotBeSameAs( m1Scoped2 );
        m2.All.Should().Contain( new IMany[] { m2Scoped, m2Scoped1, m2Scoped2 } );
    }


    // IMany will be resolved as Singleton because the auto services ManySingleton is registered.
    // This Buggy endpoint declares a IMany scoped service: this will fail when registering the StObjMap.
    [DIContainerDefinition( DIContainerKind.Endpoint )]
    public abstract class ManyAsScopedDIContainerDefinition : DIContainerDefinition<ManyAsScopedDIContainerDefinition.Data>
    {
        public sealed class Data : IScopedData
        {
        }

        public override void ConfigureContainerServices( IServiceCollection services,
                                                        Func<IServiceProvider, Data> scopeData,
                                                        IServiceProviderIsService globalServiceExists )
        {
            services.AddScoped<IActivityMonitor, ActivityMonitor>();
            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            services.AddScoped<ManyNothing>();
            services.AddScoped<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
        }
    }

    [Test]
    public void multiple_with_a_auto_computed_singleton_lifetime_cannot_be_scoped_by_endpoint_services()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManySingleton ),
                                        typeof( ManyConsumer ),
                                        typeof( ManyAsScopedDIContainerDefinition ) );
        configuration.GetFailedAutomaticServices( 
            "The IEnumerable<MultipleMappingsEndpointTests.IMany> of [IsMultiple] is a Singleton that contains externally defined Scoped mappings (endpoint 'ManyAsScoped'): 'CK.StObj.Engine.Tests.Endpoint.MultipleMappingsEndpointTests.ManyNothing'." );
    }


    // This one will be fine.
    [DIContainerDefinition( DIContainerKind.Endpoint )]
    public abstract class ManyAsSingletonDIContainerDefinition : DIContainerDefinition<ManyAsSingletonDIContainerDefinition.Data>
    {
        public sealed class Data : IScopedData
        {
        }


        public override void ConfigureContainerServices( IServiceCollection services,
                                                         Func<IServiceProvider, Data> scopeData,
                                                         IServiceProviderIsService globalServiceExists )
        {
            services.AddSingleton<ManyNothing>();
            services.AddSingleton<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
        }
    }

    [Test]
    public void DI_ISSUE_endpoints_can_register_multiple_singletons_when_the_multiple_has_been_auto_computed_as_singleton()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManySingleton ),
                                              typeof( ManyConsumer ),
                                              typeof( ManyAsSingletonDIContainerDefinition ) );
        using var auto = configuration.Run().CreateAutomaticServices();

        auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

        var e = auto.Services.GetRequiredService<DIContainerHub>().Containers.OfType<IDIContainer<ManyAsSingletonDIContainerDefinition.Data>>().Single();
        using var s1 = e.GetContainer().CreateScope();

        var manySingleton = s1.ServiceProvider.GetRequiredService<ManySingleton>();
        var manyNothingFromEndpoint = s1.ServiceProvider.GetRequiredService<ManyNothing>();

        var en = s1.ServiceProvider.GetService<IEnumerable<IMany>>();
        en.Should().Contain( [manySingleton, manyNothingFromEndpoint] );

        // The ManyConsumer is resolved from the Global service provider. The endpoint registered ManyNothing
        // is out of its scope.
        // The solution is not yet(?) obvious.
        // Should we forbid any endpoint registration of a [Multiple]S?
        // A better approach may be to fully analyze this and to propagate a "Singleton - Endpoint" trait to dependencies...
        // ManyConsumer would have to be explictly [ContainerConfiguredSingletonService]?
        var manyConsumer = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
        manyConsumer.All.Should().NotBeSameAs( en );
        manyConsumer.All.Should().Contain( [manySingleton] );
    }

    [Test]
    public void multiple_with_a_auto_computed_singleton_lifetime_cannot_be_scoped_by_global()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ManySingleton ),
                                              typeof( ManyConsumer ),
                                              typeof( ManyAsScopedDIContainerDefinition ) );
        configuration.GetFailedAutomaticServices(
           "The IEnumerable<MultipleMappingsEndpointTests.IMany> of [IsMultiple] is a Singleton that contains externally defined Scoped mappings (endpoint 'ManyAsScoped'): 'CK.StObj.Engine.Tests.Endpoint.MultipleMappingsEndpointTests.ManyNothing'.",
           configureServices: s =>
           {
                s.AddScoped<ManyNothing>();
                s.AddScoped<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
           } );
    }


}
