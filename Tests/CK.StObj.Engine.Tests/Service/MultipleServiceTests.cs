using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class MultipleServiceTests
    {
        [IsMultiple]
        public interface IHostedService : ISingletonAutoService { }

        public class S1 : IHostedService { }
        public class S2 : IHostedService { }

        [Test]
        public void simple_Multiple_services_discovery()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( S1 ) );
            collector.RegisterType( typeof( S2 ) );

            var result = TestHelper.CreateAutomaticServices( collector );
            try
            {
                result.Map.Services.SimpleMappings.ContainsKey( typeof( IHostedService ) ).Should().BeFalse();
                IStObjServiceClassDescriptor s1 = result.Map.Services.SimpleMappings[typeof( S1 )];
                IStObjServiceClassDescriptor s2 = result.Map.Services.SimpleMappings[typeof( S2 )];
                s1.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IHostedService ) } );
                s2.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IHostedService ) } );
                s1.IsScoped.Should().BeFalse( "Nothing prevents S1 to be singleton." );
                s2.IsScoped.Should().BeFalse( "Nothing prevents S2 to be singleton." );

                var hosts = result.Services.GetRequiredService<IEnumerable<IHostedService>>();
                hosts.Should().HaveCount( 2 );
            }
            finally
            {
                result.Services.Dispose();
            }
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
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( WontWork1 ) );
                // Registering it: Its CKTypeKind is None.
                collector.RegisterType( typeof( TotallyExternalClass ) );
                TestHelper.GetFailedResult( collector );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( WontWork2 ) );
                collector.RegisterType( typeof( AutoServiceImpl ) );
                TestHelper.GetFailedResult( collector );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( WontWork3 ) );
                collector.RegisterType( typeof( S2 ) );
                TestHelper.GetFailedResult( collector );
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
        public void AutoService_can_depend_on_IEnumerable_of_struct_and_this_requires_an_explicit_registration_in_the_DI_container_at_runtime()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( MayWork ) );

                using( TestHelper.Monitor.CollectEntries( out var logs, LogLevelFilter.Trace, 1000 ) )
                {
                    using var s = TestHelper.CreateAutomaticServices( collector, null ).Services;
                    var resolved = s.GetRequiredService<MayWork>();
                    resolved.Ints.Should().BeEmpty();

                    logs.Should().Contain( e => e.MaskedLevel == LogLevel.Warn
                                                && e.Text.Contains( "This requires an explicit registration in the DI container", StringComparison.Ordinal ) );
                }
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( MayWork ) );

                using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace, 1000 ) )
                {
                    var explicitInstance = new[] { 42, 3712 };

                    using var s = TestHelper.CreateAutomaticServices( collector, configureServices: services =>
                    {
                        services.Services.AddSingleton( typeof( IEnumerable<int> ), explicitInstance );

                    } ).Services;
                    var resolved = s.GetRequiredService<MayWork>();
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
        public void real_objects_can_support_IsMultiple_interfaces_but_interfaces_cannot_be_IRealObjects_and_IsMultiple()
        {
            {
                var c = TestHelper.CreateStObjCollector();
                c.RegisterType( typeof( ThatIsNotPossible ) );
                TestHelper.GetFailedResult( c );

                var c2 = TestHelper.CreateStObjCollector();
                c2.RegisterType( typeof( ThatIsNotPossible2 ) );
                TestHelper.GetFailedResult( c2 );
            }

            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( UserGoogle ) );
            collector.RegisterType( typeof( UserOffice ) );

            var result = TestHelper.CreateAutomaticServices( collector );
            Debug.Assert( result.CollectorResult.EngineMap != null, "No initialization error." );
            try
            {
                result.Map.Services.SimpleMappings.ContainsKey( typeof( IAuthProvider ) ).Should().BeFalse();
                IStObjFinalImplementation g = result.CollectorResult.EngineMap.StObjs.ToHead( typeof( IUserGoogle ) )!.FinalImplementation;
                IStObjFinalImplementation o = result.CollectorResult.EngineMap.StObjs.ToHead( typeof( UserOffice ) )!.FinalImplementation;
                g.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IAuthProvider ) } );
                o.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IAuthProvider ) } );

                var authProviders = result.Services.GetRequiredService<IEnumerable<IAuthProvider>>();
                var gS = result.Services.GetRequiredService<IUserGoogle>();
                var oS = result.Services.GetRequiredService<UserOffice>();

                authProviders.Should().BeEquivalentTo( new IAuthProvider[] { gS, oS } );

            }
            finally
            {
                result.Services.Dispose();
            }

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
        public void IAutoServices_can_depend_on_IEnumerable_of_IsMultiple_interfaces_on_RealObjects_and_is_Singleton()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( UserGoogle ) );
            collector.RegisterType( typeof( UserOffice ) );
            collector.RegisterType( typeof( MulipleConsumer ) );

            var result = TestHelper.CreateAutomaticServices( collector );
            try
            {
                result.Map.Services.SimpleMappings.ContainsKey( typeof( IAuthProvider ) ).Should().BeFalse();
                result.Map.Services.SimpleMappings[typeof( MulipleConsumer )].IsScoped.Should().BeFalse( "RealObjects are singletons." );
                var c = result.Services.GetRequiredService<MulipleConsumer>();
                var g = result.Services.GetRequiredService<IUserGoogle>();
                var o = result.Services.GetRequiredService<UserOffice>();

                c.Providers.Should().BeEquivalentTo( new IAuthProvider[] { g, o } );
            }
            finally
            {
                result.Services.Dispose();
            }
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
        public void IsMutiple_works_on_external_interfaces_and_this_is_the_magic_for_IHostedService_auto_registering()
        {
            // Here class HNot is a IOfficialHostedService but not a IAutoService and not explicitly registered: it is not automatically registered.
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService );
                collector.RegisterType( typeof( H1 ) );
                collector.RegisterType( typeof( H2 ) );
                collector.RegisterType( typeof( HNot ) );

                var result = TestHelper.CreateAutomaticServices( collector );
                try
                {
                    result.Map.Services.SimpleMappings.ContainsKey( typeof( IOfficialHostedService ) ).Should().BeFalse( "A Multiple interface IS NOT mapped." );
                    IStObjServiceClassDescriptor s1 = result.Map.Services.SimpleMappings[typeof( H1 )];
                    IStObjServiceClassDescriptor s2 = result.Map.Services.SimpleMappings[typeof( H2 )];

                    result.Map.Services.SimpleMappings.ContainsKey( typeof( HNot ) ).Should().BeFalse( "HNot is not an AutoService!" );
                    s1.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
                    s2.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
                    s1.IsScoped.Should().BeFalse( "Nothing prevents H1 to be singleton." );
                    s2.IsScoped.Should().BeTrue( "H2 is IScopedAutoService." );

                    var hosts = result.Services.GetRequiredService<IEnumerable<IOfficialHostedService>>();
                    hosts.Should().HaveCount( 2, "Only H1 and H2 are considered." );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
            // Here, the HNot totally external service is registered in the ServiceCollection (at runtime):
            // it appears in the IEnumerable<IOfficialHostedService>.
            // Of course, in this case, no lifetime analysis can be done.
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService );
                collector.RegisterType( typeof( H1 ) );
                collector.RegisterType( typeof( H2 ) );

                var result = TestHelper.CreateAutomaticServices( collector, configureServices: services =>
                {
                    // Here we use ServiceRegistrar that takes care of the registration with logs.
                    services.Register( typeof( IOfficialHostedService ), typeof( HNot ), isScoped: false, allowMultipleRegistration: true );
                    //
                    // We could also have used the standard ServiceCollection registration of the mapping:
                    // but without logs nor duplicate registration detection.
                    // services.Services.AddScoped<IOfficialHostedService, HNot>();
                } );
                try
                {
                    result.Map.Services.SimpleMappings.ContainsKey( typeof( IOfficialHostedService ) ).Should().BeFalse( "A Multiple interface IS NOT mapped." );
                    IStObjServiceClassDescriptor s1 = result.Map.Services.SimpleMappings[typeof( H1 )];
                    IStObjServiceClassDescriptor s2 = result.Map.Services.SimpleMappings[typeof( H2 )];
                    result.Map.Services.SimpleMappings.ContainsKey( typeof( HNot ) ).Should().BeFalse( "HNot is not an AutoService!" );
                    s1.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
                    s2.MultipleMappings.Should().BeEquivalentTo( new[] { typeof( IOfficialHostedService ) } );
                    s1.IsScoped.Should().BeFalse( "Nothing prevents H1 to be singleton." );
                    s2.IsScoped.Should().BeTrue( "H2 is IScopedAutoService." );

                    var hosts = result.Services.GetRequiredService<IEnumerable<IOfficialHostedService>>();
                    hosts.Should().HaveCount( 3, "H1, H2 AND HNot are now considered. HNot appear in the DI container." );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
        }

        [Test]
        public void IsMutiple_AND_IsSingleton_on_external_IHostedService_ensures_that_it_is_Singleton()
        {
            // Success: H1 is singleton.
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
                collector.RegisterType( typeof( H1 ) );
                var result = TestHelper.CreateAutomaticServices( collector );
                try
                {
                    result.Map.Services.SimpleMappings[typeof( H1 )].IsScoped.Should().BeFalse( "IOfficialHostedService makes it Singleton." );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
            // Failure: H2 is IScopedAutoService.
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IOfficialHostedService ), AutoServiceKind.IsMultipleService | AutoServiceKind.IsSingleton );
                collector.RegisterType( typeof( H2 ) );
                TestHelper.GetFailedResult( collector );
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
        public void IEnumerable_resolves_implementations_lifetimes_by_considering_final_implementations()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( ManyAuto ) );
                collector.RegisterType( typeof( ManySingleton ) );
                collector.RegisterType( typeof( ManyConsumer ) );
                var result = TestHelper.CreateAutomaticServices( collector );
                try
                {
                    result.Map.Services.SimpleMappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

                    var m = result.Services.GetRequiredService<ManyConsumer>();
                    m.All.Should().BeEquivalentTo( new IMany[] { result.Services.GetRequiredService<ManyAuto>(), result.Services.GetRequiredService<ManySingleton>() } );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( ManyAuto ) );
                collector.RegisterType( typeof( ManyScoped ) );
                collector.RegisterType( typeof( ManyConsumer ) );
                var result = TestHelper.CreateAutomaticServices( collector );
                try
                {
                    result.Map.Services.SimpleMappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

                    var m = result.Services.GetRequiredService<ManyConsumer>();
                    m.All.Should().BeEquivalentTo( new IMany[] { result.Services.GetRequiredService<ManyAuto>(), result.Services.GetRequiredService<ManyScoped>() } );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( ManyAuto ) );
                collector.RegisterType( typeof( ManyScoped ) );
                collector.RegisterType( typeof( ManySingleton ) );
                collector.RegisterType( typeof( ManyConsumer ) );
                var result = TestHelper.CreateAutomaticServices( collector );
                try
                {
                    result.Map.Services.SimpleMappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

                    var m = result.Services.GetRequiredService<ManyConsumer>();
                    m.All.Should().HaveCount( 3 )
                                  .And.Contain( result.Services.GetRequiredService<ManyAuto>() )
                                  .And.Contain( result.Services.GetRequiredService<ManySingleton>() )
                                  .And.Contain( result.Services.GetRequiredService<ManyScoped>() );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
        }

        [Test]
        public void IEnumerable_Kind_can_be_explictly_configured_via_SetAutoServiceKind()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IEnumerable<IMany> ), AutoServiceKind.IsScoped );
                collector.RegisterType( typeof( ManyAuto ) );
                collector.RegisterType( typeof( ManySingleton ) );
                collector.RegisterType( typeof( ManyConsumer ) );
                var result = TestHelper.CreateAutomaticServices( collector );
                try
                {
                    result.Map.Services.SimpleMappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Could be resolved as Singleton, but Scoped as stated." );

                    var m = result.Services.GetRequiredService<ManyConsumer>();
                    m.All.Should().BeEquivalentTo( new IMany[] { result.Services.GetRequiredService<ManyAuto>(), result.Services.GetRequiredService<ManySingleton>() } );
                }
                finally
                {
                    result.Services.Dispose();
                }
            }
        }

        [Test]
        public void IEnumerable_cannot_be_SetAutoServiceKind_Singleton_if_the_enumerated_interface_is_Scoped()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( IEnumerable<IMany> ), AutoServiceKind.IsSingleton );
            collector.SetAutoServiceKind( typeof( IMany ), AutoServiceKind.IsScoped );
            collector.RegisterType( typeof( ManyAuto ) );
            collector.RegisterType( typeof( ManySingleton ) );
            collector.RegisterType( typeof( ManyConsumer ) );
            TestHelper.GetFailedResult( collector );
        }

    }
}
