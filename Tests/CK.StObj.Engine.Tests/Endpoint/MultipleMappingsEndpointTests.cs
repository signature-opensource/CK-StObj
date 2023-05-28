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
        // Will be scoped.
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
        public abstract class FirstEndpointDefinition : EndpointDefinition<string>
        {
            public override void ConfigureEndpointServices( IServiceCollection services, IServiceProviderIsService globalServiceExists )
            {
            }
        }

        [EndpointDefinition]
        public abstract class SecondEndpointDefinition : EndpointDefinition<int>
        {
            public override void ConfigureEndpointServices( IServiceCollection services, IServiceProviderIsService globalServiceExists )
            {
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
                var e1 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<string>>().Single();
                var e2 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<int>>().Single();
                using var s1 = e1.GetContainer().CreateScope( "Scoped Data" );
                using var s2 = e2.GetContainer().CreateScope( 3712 );

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
                var e1 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<string>>().Single();
                var e2 = g.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<int>>().Single();
                using var s1 = e1.GetContainer().CreateScope( "Scoped Data" );
                using var s2 = e2.GetContainer().CreateScope( 3712 );

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
                var e1 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<string>>().Single();
                var e2 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<int>>().Single();
                using var s1 = e1.GetContainer().CreateScope( "Scoped Data" );
                using var s2 = e2.GetContainer().CreateScope( 3712 );

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
        public async Task multiple_scoped_with_external_service_Async()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( ManyScoped ),
                                                             typeof( ManyScoped2 ),
                                                             typeof( ManyNothing ),
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
                var e1 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<string>>().Single();
                var e2 = g.ServiceProvider.GetRequiredService<EndpointTypeManager>().EndpointTypes.OfType<IEndpointType<int>>().Single();
                using var s1 = e1.GetContainer().CreateScope( "Scoped Data" );
                using var s2 = e2.GetContainer().CreateScope( 3712 );

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

    }
}
