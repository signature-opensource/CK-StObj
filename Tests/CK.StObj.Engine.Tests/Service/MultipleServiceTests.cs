using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

[TestFixture]
public class MultipleServiceTests
{
    [IsMultiple]
    public interface IHostedService : ISingletonAutoService { }

    public class S1 : IHostedService { }
    public class S2 : IHostedService { }

    [Test]
    public async Task simple_Multiple_services_discovery_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( S1 ), typeof( S2 ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Map.Services.Mappings.ContainsKey( typeof( IHostedService ) ).Should().BeFalse();
        IStObjServiceClassDescriptor s1 = auto.Map.Services.Mappings[typeof( S1 )];
        IStObjServiceClassDescriptor s2 = auto.Map.Services.Mappings[typeof( S2 )];
        s1.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IHostedService ) } );
        s2.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IHostedService ) } );
        s1.IsScoped.Should().BeFalse( "Nothing prevents S1 to be singleton." );
        s2.IsScoped.Should().BeFalse( "Nothing prevents S2 to be singleton." );

        var hosts = auto.Services.GetRequiredService<IEnumerable<IHostedService>>();
        hosts.Should().HaveCount( 2 );
    }

    public class TotallyExternalClass { }

    public class AutoServiceImpl : IAutoService { }

    public class WontWork1 : IAutoService
    {
        public WontWork1( IEnumerable<TotallyExternalClass> all ) { }
    }

    public class WontWork2 : IAutoService
    {
        public WontWork2( IEnumerable<AutoServiceImpl> all ) { }
    }

    // S2 is the implementation of a [IsMultiple] IAutoService.
    public class WontWork3 : IAutoService
    {
        public WontWork3( IEnumerable<S2> all ) { }
    }

    [Test]
    public void AutoService_cannot_depend_on_IEnumerable_of_any_kind_of_class()
    {
        {
            // Registering TotallyExternalClass: Its CKTypeKind is None.
            TestHelper.GetFailedCollectorResult( [typeof( WontWork1 ), typeof( TotallyExternalClass )], "IEnumerable<T> requires that T is a [IsMultiple] interface. In no way can T be a class." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( WontWork2 ), typeof( AutoServiceImpl )], "IEnumerable<T> requires that T is a [IsMultiple] interface. In no way can T be a class." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( WontWork3 ), typeof( S2 )], "IEnumerable<T> requires that T is a [IsMultiple] interface. In no way can T be a class." );
        }
    }

    public class MayWork : IAutoService
    {
        public MayWork( IEnumerable<int> all )
        {
            Ints = all;
        }
        public IEnumerable<int> Ints { get; }
    }

    [Test]
    public async Task AutoService_can_depend_on_IEnumerable_of_struct_and_this_requires_an_explicit_registration_in_the_DI_container_at_runtime_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( MayWork ) );

            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
            {
                await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
                var resolved = auto.Services.GetRequiredService<MayWork>();
                resolved.Ints.Should().BeEmpty();

                entries.Should().Contain( e => e.MaskedLevel == LogLevel.Warn
                                            && e.Text.Contains( "This requires an explicit registration in the DI container", StringComparison.Ordinal ) );
            }
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( MayWork ) );

            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
            {
                var explicitInstance = new[] { 42, 3712 };

                await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: services =>
                {
                    services.AddSingleton( typeof( IEnumerable<int> ), explicitInstance );

                } );
                var resolved = auto.Services.GetRequiredService<MayWork>();
                resolved.Ints.Should().BeSameAs( explicitInstance );

                entries.Should().Contain( e => e.MaskedLevel == LogLevel.Warn
                                            && e.Text.Contains( "This requires an explicit registration in the DI container", StringComparison.Ordinal ) );
            }
        }

    }

    [IsMultiple]
    public interface IAuthProvider { }

    public interface IUserGoogle : IRealObject, IAuthProvider { }

    public class UserGoogle : IUserGoogle
    {
    }

    public class UserOffice : IRealObject, IAuthProvider
    {
    }

    [IsMultiple]
    public interface IAmAMultipleRealObject : IRealObject { }

    [IsMultiple]
    public interface IAmAMultipleRealObject2 : IUserGoogle { }

    public class ThatIsNotPossible : IAmAMultipleRealObject { }
    public class ThatIsNotPossible2 : IAmAMultipleRealObject2 { }

    [Test]
    public async Task real_objects_can_support_IsMultiple_interfaces_but_interfaces_cannot_be_IRealObjects_and_IsMultiple_Async()
    {
        {
            var c = new[] { typeof( ThatIsNotPossible ) };
            TestHelper.GetFailedCollectorResult( c, "IRealObject interface cannot be marked as a Multiple service" );

            var c2 = new[] { typeof( ThatIsNotPossible2 ) };
            TestHelper.GetFailedCollectorResult( c2, "IRealObject interface cannot be marked as a Multiple service" );
        }

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( UserGoogle ), typeof( UserOffice ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Map.Services.Mappings.ContainsKey( typeof( IAuthProvider ) ).Should().BeFalse();
        IStObjFinalImplementation g = auto.Map.StObjs.ToLeaf( typeof( IUserGoogle ) )!;
        IStObjFinalImplementation o = auto.Map.StObjs.ToLeaf( typeof( UserOffice ) )!;
        g.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IAuthProvider ) } );
        o.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IAuthProvider ) } );

        var authProviders = auto.Services.GetRequiredService<IEnumerable<IAuthProvider>>();
        var gS = auto.Services.GetRequiredService<IUserGoogle>();
        var oS = auto.Services.GetRequiredService<UserOffice>();

        authProviders.Should().BeEquivalentTo( new IAuthProvider[] { gS, oS } );
    }

    public class MulipleConsumer : IAutoService
    {
        public MulipleConsumer( IEnumerable<IAuthProvider> providers )
        {
            providers.Should().HaveCount( 2 );
            Providers = providers.ToArray();
        }

        public IAuthProvider[] Providers { get; }
    }

    [Test]
    public async Task IAutoServices_can_depend_on_IEnumerable_of_IsMultiple_interfaces_on_RealObjects_and_is_Singleton_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( UserGoogle ), typeof( UserOffice ), typeof( MulipleConsumer ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Map.Services.Mappings.ContainsKey( typeof( IAuthProvider ) ).Should().BeFalse();
        auto.Map.Services.Mappings[typeof( MulipleConsumer )].IsScoped.Should().BeFalse( "RealObjects are singletons." );
        var c = auto.Services.GetRequiredService<MulipleConsumer>();
        var g = auto.Services.GetRequiredService<IUserGoogle>();
        var o = auto.Services.GetRequiredService<UserOffice>();

        c.Providers.Should().BeEquivalentTo( new IAuthProvider[] { g, o } );
    }

    /// <summary>
    /// This interface is NOT a IAutoService, just like IHostedService.
    /// But it is declared as a AutoServiceKind.IsMultipleService.
    /// </summary>
    public interface IOfficialHostedService { }

    public class H1 : IOfficialHostedService, IAutoService { }
    public class H2 : IOfficialHostedService, IScopedAutoService { }
    public class HNot : IOfficialHostedService { }

    [Test]
    public async Task IsMutiple_works_on_external_interfaces_and_this_is_the_magic_for_IHostedService_auto_registering_Async()
    {
        // Here class HNot is a IOfficialHostedService but not a IAutoService and not explicitly registered: it is not automatically registered.
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IOfficialHostedService ), ConfigurableAutoServiceKind.IsMultipleService );
            configuration.FirstBinPath.Types.Add( typeof( H1 ), typeof( H2 ), typeof( HNot ) );

            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Map.Services.Mappings.ContainsKey( typeof( IOfficialHostedService ) ).Should().BeFalse( "A Multiple interface IS NOT mapped." );
            IStObjServiceClassDescriptor s1 = auto.Map.Services.Mappings[typeof( H1 )];
            IStObjServiceClassDescriptor s2 = auto.Map.Services.Mappings[typeof( H2 )];

            auto.Map.Services.Mappings.ContainsKey( typeof( HNot ) ).Should().BeFalse( "HNot is not an AutoService!" );
            s1.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
            s2.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
            s1.IsScoped.Should().BeFalse( "Nothing prevents H1 to be singleton." );
            s2.IsScoped.Should().BeTrue( "H2 is IScopedAutoService." );

            var hosts = auto.Services.GetRequiredService<IEnumerable<IOfficialHostedService>>();
            hosts.Should().HaveCount( 2, "Only H1 and H2 are considered." );
        }
        // Here, the HNot totally external service is registered in the ServiceCollection (at runtime):
        // it appears in the IEnumerable<IOfficialHostedService>.
        // Of course, in this case, no lifetime analysis can be done.
        {

            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( new TypeConfiguration( typeof( IOfficialHostedService ), ConfigurableAutoServiceKind.IsMultipleService ) );
            configuration.FirstBinPath.Types.Add( typeof( H1 ), typeof( H2 ) );

            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices( configureServices: services =>
            {
                services.AddSingleton<IOfficialHostedService, HNot>();
            } );

            auto.Map.Services.Mappings.ContainsKey( typeof( IOfficialHostedService ) ).Should().BeFalse( "A Multiple interface IS NOT mapped." );
            IStObjServiceClassDescriptor s1 = auto.Map.Services.Mappings[typeof( H1 )];
            IStObjServiceClassDescriptor s2 = auto.Map.Services.Mappings[typeof( H2 )];
            auto.Map.Services.Mappings.ContainsKey( typeof( HNot ) ).Should().BeFalse( "HNot is not an AutoService!" );
            s1.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
            s2.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
            s1.IsScoped.Should().BeFalse( "Nothing prevents H1 to be singleton." );
            s2.IsScoped.Should().BeTrue( "H2 is IScopedAutoService." );

            var hosts = auto.Services.GetRequiredService<IEnumerable<IOfficialHostedService>>();
            hosts.Should().HaveCount( 3, "H1, H2 AND HNot are now considered. HNot appear in the DI container." );
        }
    }

    [Test]
    public async Task IsMutiple_AND_IsSingleton_on_external_IHostedService_ensures_that_it_is_Singleton_Async()
    {
        // Success: H1 is singleton.
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IOfficialHostedService ), ConfigurableAutoServiceKind.IsMultipleService | ConfigurableAutoServiceKind.IsSingleton );
            configuration.FirstBinPath.Types.Add( typeof( H1 ) );

            var result = (await configuration.RunAsync().ConfigureAwait(false)).CreateAutomaticServices();
            result.Map.Services.Mappings[typeof( H1 )].IsScoped.Should().BeFalse( "IOfficialHostedService makes it Singleton." );
        }
        // Failure: H2 is IScopedAutoService.
        {
            var config = TestHelper.CreateDefaultEngineConfiguration();
            config.FirstBinPath.Types.Add( typeof( IOfficialHostedService ), ConfigurableAutoServiceKind.IsMultipleService | ConfigurableAutoServiceKind.IsSingleton );
            config.FirstBinPath.Types.Add( typeof( H2 ) );

            await config.GetFailedAutomaticServicesAsync( "An interface or an implementation cannot be both Scoped and Singleton" );
        }
    }

    [IsMultiple]
    public interface IMany { }

    public class ManyScoped : IMany, IScopedAutoService { }
    public class ManySingleton : IMany, ISingletonAutoService { }
    public class ManyAuto : IMany, IAutoService { }
    public class ManyNothing : IMany { }

    public class ManyConsumer : IAutoService
    {
        public ManyConsumer( IEnumerable<IMany> all )
        {
            All = all;
        }
        public IEnumerable<IMany> All { get; }
    }

    [Test]
    public async Task IEnumerable_resolves_implementations_lifetimes_by_considering_final_implementations_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ManyAuto ), typeof( ManySingleton ), typeof( ManyConsumer ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

            var m = auto.Services.GetRequiredService<ManyConsumer>();
            m.All.Should().BeEquivalentTo( new IMany[] { auto.Services.GetRequiredService<ManyAuto>(), auto.Services.GetRequiredService<ManySingleton>() } );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ManyAuto ), typeof( ManyScoped ), typeof( ManyConsumer ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

            var m = auto.Services.GetRequiredService<ManyConsumer>();
            m.All.Should().BeEquivalentTo( new IMany[] { auto.Services.GetRequiredService<ManyAuto>(), auto.Services.GetRequiredService<ManyScoped>() } );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ManyAuto ), typeof( ManyScoped ), typeof( ManySingleton ), typeof( ManyConsumer ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

            var m = auto.Services.GetRequiredService<ManyConsumer>();
            m.All.Should().HaveCount( 3 )
                            .And.Contain( auto.Services.GetRequiredService<ManyAuto>() )
                            .And.Contain( auto.Services.GetRequiredService<ManySingleton>() )
                            .And.Contain( auto.Services.GetRequiredService<ManyScoped>() );
        }
    }

    [Test]
    public async Task IEnumerable_Kind_can_be_explicitly_configured_via_SetAutoServiceKind_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IEnumerable<IMany> ), ConfigurableAutoServiceKind.IsScoped );
        configuration.FirstBinPath.Types.Add( typeof( ManyAuto ), typeof( ManySingleton ), typeof( ManyConsumer ) );

        await using var auto = (await configuration.RunAsync().ConfigureAwait(false)).CreateAutomaticServices();
        auto.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Could be resolved as Singleton, but Scoped as stated." );

        var m = auto.Services.GetRequiredService<ManyConsumer>();
        m.All.Should().BeEquivalentTo( new IMany[] { auto.Services.GetRequiredService<ManyAuto>(), auto.Services.GetRequiredService<ManySingleton>() } );
    }

    [Test]
    public async Task IEnumerable_cannot_be_SetAutoServiceKind_Singleton_if_the_enumerated_interface_is_Scoped_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( new TypeConfiguration( typeof( IEnumerable<IMany> ), ConfigurableAutoServiceKind.IsSingleton ) );
        configuration.FirstBinPath.Types.Add( new TypeConfiguration( typeof( IMany ), ConfigurableAutoServiceKind.IsScoped ) );
        configuration.FirstBinPath.Types.Add( typeof( ManyAuto ), typeof( ManySingleton ), typeof( ManyConsumer ) );

        await configuration.GetFailedAutomaticServicesAsync( "An interface or an implementation cannot be both Scoped and Singleton" );
    }

}
