using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint
{
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

        [EndpointDefinition]
        public abstract class FirstEndpointDefinition : EndpointDefinition<FirstEndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

            public override void ConfigureEndpointServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor, ActivityMonitor>();
                services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            }
        }

        [EndpointDefinition]
        public abstract class SecondEndpointDefinition : EndpointDefinition<SecondEndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

            public override void ConfigureEndpointServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor, ActivityMonitor>();
                services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            }
        }

        [Test]
        public async Task single_singleton_Async()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManyAuto ),
                                                             typeof( ManyConsumer ),
                                                             typeof( FirstEndpointDefinition ),
                                                             typeof( SecondEndpointDefinition ) );
            var result = TestHelper.CreateAutomaticServices( collector );
            await TestHelper.StartHostedServicesAsync( result.Services );
            try
            {
                result.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

                var g = result.Services;
                var e1 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<FirstEndpointDefinition.Data>>().Single();
                var e2 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<SecondEndpointDefinition.Data>>().Single();
                using var s1 = e1.GetContainer().CreateScope();
                using var s2 = e2.GetContainer().CreateScope();

                var mG = g.GetRequiredService<ManyConsumer>();
                mG.All.Should().BeEquivalentTo( new IMany[] { g.GetRequiredService<ManyAuto>() } );

                var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
                m1.All.Should().BeEquivalentTo( mG.All );

                var m2 = s2.ServiceProvider.GetRequiredService<ManyConsumer>();
                m2.All.Should().BeEquivalentTo( mG.All );
            }
            finally
            {
                await result.Services.DisposeAsync();
            }
        }

        [Test]
        public async Task multiple_singletons_Async()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManyAuto ),
                                                             typeof( ManySingleton ),
                                                             typeof( ManyAuto2 ),
                                                             typeof( ManySingleton2 ),
                                                             typeof( ManyConsumer ),
                                                             typeof( FirstEndpointDefinition ),
                                                             typeof( SecondEndpointDefinition ) );
            var result = TestHelper.CreateAutomaticServices( collector );
            await TestHelper.StartHostedServicesAsync( result.Services );
            try
            {
                result.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeFalse( "Resolved as Singleton." );

                var g = result.Services;
                var e1 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<FirstEndpointDefinition.Data>>().Single();
                var e2 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<SecondEndpointDefinition.Data>>().Single();
                using var s1 = e1.GetContainer().CreateScope();
                using var s2 = e2.GetContainer().CreateScope();

                var mG = g.GetRequiredService<ManyConsumer>();
                mG.All.Should().BeEquivalentTo( new IMany[] { g.GetRequiredService<ManyAuto>(),
                                                              g.GetRequiredService<ManySingleton>(),
                                                              g.GetRequiredService<ManyAuto2>(),
                                                              g.GetRequiredService<ManySingleton2>() } );

                var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
                m1.All.Should().BeEquivalentTo( mG.All );

                var m2 = s2.ServiceProvider.GetRequiredService<ManyConsumer>();
                m2.All.Should().BeEquivalentTo( mG.All );
            }
            finally
            {
                await result.Services.DisposeAsync();
            }
        }

        [Test]
        public async Task single_scoped_Async()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManyScoped ),
                                                             typeof( ManyConsumer ),
                                                             typeof( FirstEndpointDefinition ),
                                                             typeof( SecondEndpointDefinition ) );
            var result = TestHelper.CreateAutomaticServices( collector );
            await TestHelper.StartHostedServicesAsync( result.Services );
            try
            {
                result.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

                using var g = result.Services.CreateScope();
                var e1 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<FirstEndpointDefinition.Data>>().Single();
                var e2 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<SecondEndpointDefinition.Data>>().Single();
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
            finally
            {
                await result.Services.DisposeAsync();
            }
        }

        [Test]
        public async Task global_can_register_multiple_services_Async()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManyScoped ),
                                                             typeof( ManyScoped2 ),
                                                             typeof( ManyConsumer ),
                                                             typeof( FirstEndpointDefinition ),
                                                             typeof( SecondEndpointDefinition ) );
            var result = TestHelper.CreateAutomaticServices( collector,
                                                             configureServices: s =>
                                                             {
                                                                 s.Services.AddScoped<ManyNothing>();
                                                                 s.Services.AddScoped<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
                                                             } );
            await TestHelper.StartHostedServicesAsync( result.Services );
            try
            {
                result.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Scoped." );

                using var g = result.Services.CreateScope();
                var e1 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<FirstEndpointDefinition.Data>>().Single();
                var e2 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<SecondEndpointDefinition.Data>>().Single();
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
            finally
            {
                await result.Services.DisposeAsync();
            }
        }


        // IMany will be resolved as Singleton because the auto services ManySingleton is registered.
        // This Buggy endpoint declares a IMany scoped service: this will fail when registering the StObjMap.
        [EndpointDefinition]
        public abstract class ManyAsScopedEndpointDefinition : EndpointDefinition<ManyAsScopedEndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

            public override void ConfigureEndpointServices( IServiceCollection services,
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
            var collector = TestHelper.CreateStObjCollector( typeof( ManySingleton ),
                                                             typeof( ManyConsumer ),
                                                             typeof( ManyAsScopedEndpointDefinition ) );
            TestHelper.GetFailedAutomaticServicesConfiguration( collector );
        }


        // This one will be fine.
        [EndpointDefinition]
        public abstract class ManyAsSingletonEndpointDefinition : EndpointDefinition<ManyAsSingletonEndpointDefinition.Data>
        {
            public sealed class Data : ScopedData
            {
                public Data( EndpointUbiquitousInfo ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }


            public override void ConfigureEndpointServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddSingleton<ManyNothing>();
                services.AddSingleton<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
            }
        }

        public async Task endpoints_can_register_multiple_singletons_when_the_multiple_has_been_auto_computed_as_singleton_Async()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManySingleton),
                                                             typeof( ManyConsumer ),
                                                             typeof( ManyAsSingletonEndpointDefinition ) );
            var result = TestHelper.CreateAutomaticServices( collector );
            await TestHelper.StartHostedServicesAsync( result.Services );
            try
            {
                result.Map.Services.Mappings[typeof( ManyConsumer )].IsScoped.Should().BeTrue( "Resolved as Singleton." );

                var e = result.Services.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<ManyAsSingletonEndpointDefinition.Data>>().Single();
                using var s1 = e.GetContainer().CreateScope();

                var m1 = s1.ServiceProvider.GetRequiredService<ManyConsumer>();
                var m1Auto = s1.ServiceProvider.GetRequiredService<ManySingleton>();
                var m1Endpoint = s1.ServiceProvider.GetRequiredService<ManyNothing>();
                m1.All.Should().Contain( new IMany[] { m1Auto, m1Endpoint } );
            }
            finally
            {
                await result.Services.DisposeAsync();
            }
        }

        [Test]
        public void multiple_with_a_auto_computed_singleton_lifetime_cannot_be_scoped_by_global()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManySingleton ),
                                                             typeof( ManyConsumer ),
                                                             typeof( ManyAsScopedEndpointDefinition ) );
            var result = TestHelper.GetFailedAutomaticServicesConfiguration( collector,
                                                                             configureServices: s =>
                                                                             {
                                                                                 s.Services.AddScoped<ManyNothing>();
                                                                                 s.Services.AddScoped<IMany, ManyNothing>( sp => sp.GetRequiredService<ManyNothing>() );
                                                                             } );
        }


    }
}
