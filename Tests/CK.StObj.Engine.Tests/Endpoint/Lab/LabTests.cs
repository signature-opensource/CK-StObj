using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    [TestFixture]
    public partial class LabTests
    {

        public interface IA
        {
            string Name { get; }
        }

        public interface IB : IA
        {
        }

        public class A : IA
        {
            public A( string name ) => Name = name;

            public string Name { get; }
        }

        public class B : A, IB
        {
            public B( string name ) : base( name ) { }
        }

        class Scoped
        {
            public Scoped( IB b, IEnumerable<A> multipleA )
            {
                B = b;
                MultipleA = multipleA;
            }

            public IB B { get; }

            public IEnumerable<A> MultipleA { get; }
        }

        [Test]
        public void relay_to_global_DI_explained()
        {
            ServiceCollection global = new ServiceCollection();
            FakeHost.ConfigureGlobal( global );

            // B is "normal".
            global.AddSingleton( new B( "B instance" ) );
            global.AddSingleton<IB, B>( sp => sp.GetRequiredService<B>() );
            // A is registered twice.
            global.AddSingleton( new A( "A instance" ) );
            global.AddSingleton( sp => new A( "A instance by factory" ) );
            // This scoped must be bound to the B and the two A instance.
            global.AddScoped<Scoped>();


            IEndpointServiceProvider<FakeEndpointDefinition.Data>? e = FakeHost.CreateServiceProvider( TestHelper.Monitor, global, out var g );
            Debug.Assert( e != null && g != null );

            using var scopedG = g.CreateScope();
            // From the global, obtains a EndpointUbiquitousInfo.
            var ubiq = scopedG.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();

            using var scopedE = e.CreateAsyncScope( new FakeEndpointDefinition.Data( ubiq, TestHelper.Monitor ) );

            (A A, B B, IEnumerable<A> MultiA, Scoped S) fromE;
            (A A, B B, IEnumerable<A> MultiA, Scoped S) fromG;
            fromE = ResolveFrom( scopedE.ServiceProvider );
            fromG = ResolveFrom( scopedG.ServiceProvider );

            fromE.A.Should().BeSameAs( fromG.A );
            fromE.B.Should().BeSameAs( fromG.B );
            fromE.MultiA.Should().NotBeSameAs( fromG.MultiA, "Unfortunately... But its content is okay, and anyway, see below: " +
                                                             "instances are always different for resolved IEnumerable<>." );
            fromE.MultiA.Select( a => a.Name ).Should().BeEquivalentTo( new[] { "A instance", "A instance by factory" } );
            fromE.MultiA.ElementAt( 0 ).Should().BeSameAs( fromG.MultiA.ElementAt( 0 ) );
            fromE.MultiA.ElementAt( 1 ).Should().BeSameAs( fromG.MultiA.ElementAt( 1 ) );

            fromE.S.Should().NotBeSameAs( fromG.S, "Scoped are obviously different instances." );
            fromE.S.B.Should().BeSameAs( fromG.S.B ).And.BeSameAs( fromG.B );
            fromE.S.MultipleA.Should().BeSameAs( fromE.MultiA );
            fromG.S.MultipleA.Should().NotBeSameAs( fromG.MultiA, "Each resolution of IEnumerable<> leads to a different instance." );

            // When we ask for IEnumerable<B>, we always have a different (single) enumeration.
            var gB = scopedG.ServiceProvider.GetRequiredService<IEnumerable<B>>();
            gB.Should().NotBeSameAs( scopedG.ServiceProvider.GetRequiredService<IEnumerable<B>>() );
            var eB = scopedG.ServiceProvider.GetRequiredService<IEnumerable<B>>();
            eB.Should().NotBeSameAs( scopedE.ServiceProvider.GetRequiredService<IEnumerable<B>>() );

            gB.Should().HaveCount( 1 ).And.Contain( new[] { e.GetRequiredService<B>() } );
            eB.Should().HaveCount( 1 ).And.Contain( new[] { e.GetRequiredService<B>() } );

            //
            using var scopedG2 = g.CreateScope();
            using var scopedE2 = e.CreateAsyncScope( new FakeEndpointDefinition.Data( ubiq, TestHelper.Monitor ) );

            var fromE2 = ResolveFrom( scopedE2.ServiceProvider );
            var fromG2 = ResolveFrom( scopedG2.ServiceProvider );

            fromG2.A.Should().BeSameAs( fromG.A );
            fromG2.B.Should().BeSameAs( fromG.B );
            fromG2.S.Should().NotBeSameAs( fromG.S );

            fromE2.A.Should().BeSameAs( fromE.A ).And.BeSameAs( fromG.A );
            fromE2.B.Should().BeSameAs( fromE.B ).And.BeSameAs( fromG.B );
            fromE2.S.Should().NotBeSameAs( fromE.S );

            static (A A, B B, IEnumerable<A> MultiA, Scoped S) ResolveFrom( IServiceProvider sp )
            {
                return (sp.GetRequiredService<A>(), sp.GetRequiredService<B>(), sp.GetRequiredService<IEnumerable<A>>(), sp.GetRequiredService<Scoped>());
            }
        }

        interface IMulti
        {
            public string Name => GetType().Name;
        }
        interface IMultiSing
        {
            public string Name => GetType().Name;
        }
        class Sing1 : IMulti, IMultiSing { }
        class Sing2 : IMulti, IMultiSing { }
        class Scop1 : IMulti { }
        class Scop2 : IMulti { }

        [TestCase( true )]
        [TestCase( false )]
        public void multiple_registrations_with_relay_work( bool fromRoot )
        {
            var services = new ServiceCollection();
            FakeHost.ConfigureGlobal( services );

            services.AddSingleton<Sing1>();
            // This is how a multiple registration should be done: with the
            // service type and the final type mapping.
            services.AddSingleton<IMultiSing, Sing1>( sp => sp.GetRequiredService<Sing1>() );
            services.AddSingleton<Sing2>();
            // This works because the IEnumerable is handled at the registration level
            // in the container but this cannot be analyzed and the final mapped type
            // is lost.
            services.AddSingleton<IMultiSing>( sp => sp.GetRequiredService<Sing2>() );

            var g = services.BuildServiceProvider();
            using var scopedG = g.CreateScope();

            var sp = fromRoot ? g : scopedG.ServiceProvider;

            var sing1 = sp.GetRequiredService<Sing1>();
            var sing2 = sp.GetRequiredService<Sing2>();
            var multi = sp.GetRequiredService<IEnumerable<IMultiSing>>();
            multi.Select( m => m.Name ).Should().BeEquivalentTo( new[] { "Sing1", "Sing2" } );
            // The multiple singletons are the ones.
            multi.OfType<Sing1>().Single().Should().BeSameAs( sing1 );
            multi.OfType<Sing2>().Single().Should().BeSameAs( sing2 );
        }

        [TestCase( true )]
        [TestCase( false )]
        public void multiple_registrations_with_TryAddEnumerable_do_not_work( bool fromRoot )
        {
            var services = new ServiceCollection();
            services.AddSingleton<Sing1>();
            services.TryAddEnumerable( ServiceDescriptor.Singleton<IMultiSing, Sing1>() );
            services.AddSingleton<Sing2>();
            services.TryAddEnumerable( ServiceDescriptor.Singleton<IMultiSing, Sing2>() );
            var g = services.BuildServiceProvider();
            using var scopedG = g.CreateScope();

            var sp = fromRoot ? g : scopedG.ServiceProvider;

            var sing1 = sp.GetRequiredService<Sing1>();
            var sing2 = sp.GetRequiredService<Sing2>();
            var multi = sp.GetRequiredService<IEnumerable<IMultiSing>>();
            // Seems okay but...
            multi.Select( m => m.Name ).Should().BeEquivalentTo( new[] { "Sing1", "Sing2" } );
            // ...the multiple singletons are NOT the same.
            multi.OfType<Sing1>().Single().Should().NotBeSameAs( sing1 );
            multi.OfType<Sing2>().Single().Should().NotBeSameAs( sing2 );
        }

        [Test]
        public void hybrid_lifetime_multiple_registrations_in_endpoint_containers()
        {
            var global = new ServiceCollection();
            FakeHost.ConfigureGlobal( global );

            global.AddSingleton<Sing1>();
            // This is the ONLY way to register a multiple mapping:
            // The returned type of the lambda can be used to determine the ImplementationType.
            global.AddSingleton<IMulti, Sing1>( sp => sp.GetRequiredService<Sing1>() );
            global.AddSingleton<IMultiSing, Sing1>( sp => sp.GetRequiredService<Sing1>() );
            global.AddSingleton<Sing2>();

            // This works too (but doesn't bring much to the table).
            global.AddSingleton<IMulti>( (Func<IServiceProvider, Sing2>)(sp => sp.GetRequiredService<Sing2>()) );
            global.AddSingleton<IMultiSing>( (Func<IServiceProvider, Sing2>)(sp => sp.GetRequiredService<Sing2>()) );

            global.AddScoped<Scop1>();
            global.AddScoped<IMulti, Scop1>( sp => sp.GetRequiredService<Scop1>() );
            global.AddScoped<Scop2>();
            global.AddScoped<IMulti, Scop2>( sp => sp.GetRequiredService<Scop2>() );

            IEndpointServiceProvider<FakeEndpointDefinition.Data>? e = FakeHost.CreateServiceProvider( TestHelper.Monitor, global, out var g );
            Debug.Assert( e != null && g != null );

            using var scopedG = g.CreateScope();
            var ubiq = scopedG.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
            using var scopedE = e.CreateAsyncScope( new FakeEndpointDefinition.Data( ubiq, TestHelper.Monitor ) );

            // Both containers resolves to the same instance.
            var sing1 = CheckTrueSingleton<Sing1>( g, e, scopedG, scopedE );

            var mG = scopedG.ServiceProvider.GetServices<IMulti>();
            mG.Select( m => m.Name ).Should().BeEquivalentTo( new[] { "Sing1", "Sing2", "Scop1", "Scop2" } );
            mG.OfType<Sing1>().Single().Should().BeSameAs( sing1 );

            var sing2 = CheckTrueSingleton<Sing2>( g, e, scopedG, scopedE );
            mG.OfType<Sing2>().Single().Should().BeSameAs( sing2 );

            // The multi from the endpoint container is the complex one.
            var mE = scopedE.ServiceProvider.GetServices<IMulti>();
            mE.Select( m => m.Name ).Should().BeEquivalentTo( new[] { "Sing1", "Sing2", "Scop1", "Scop2" } );
            mE.OfType<Sing1>().Single().Should().BeSameAs( sing1 );
            mE.OfType<Sing2>().Single().Should().BeSameAs( sing2 );
            // The scoped from the 2 scopes are not the same.
            mE.OfType<Scop1>().Single().Should().NotBeSameAs( mG.OfType<Scop1>().Single() );
            mE.OfType<Scop2>().Single().Should().NotBeSameAs( mG.OfType<Scop2>().Single() );

            // Creating other scopes.
            using var scopedG2 = g.CreateScope();
            using var scopedE2 = e.CreateScope();

            var mG2 = scopedG2.ServiceProvider.GetServices<IMulti>();
            var mE2 = scopedE2.ServiceProvider.GetServices<IMulti>();

            mE2.OfType<Sing1>().Single().Should().BeSameAs( sing1 );
            mE2.OfType<Sing2>().Single().Should().BeSameAs( sing2 );
            mE2.OfType<Scop1>().Single().Should().NotBeSameAs( mG2.OfType<Scop1>().Single() );
            mE2.OfType<Scop2>().Single().Should().NotBeSameAs( mG2.OfType<Scop2>().Single() );
            mE2.OfType<Scop1>().Single().Should().NotBeSameAs( mE.OfType<Scop1>().Single() );
            mE2.OfType<Scop2>().Single().Should().NotBeSameAs( mE.OfType<Scop2>().Single() );

            mG2.OfType<Sing1>().Single().Should().BeSameAs( sing1 );
            mG2.OfType<Sing2>().Single().Should().BeSameAs( sing2 );
            mG2.OfType<Scop1>().Single().Should().NotBeSameAs( mG.OfType<Scop1>().Single() );
            mG2.OfType<Scop2>().Single().Should().NotBeSameAs( mG.OfType<Scop2>().Single() );
            mG2.OfType<Scop1>().Single().Should().NotBeSameAs( mE2.OfType<Scop1>().Single() );
            mG2.OfType<Scop2>().Single().Should().NotBeSameAs( mE2.OfType<Scop2>().Single() );

            static T CheckTrueSingleton<T>( IServiceProvider g, IServiceProvider e, IServiceScope scopedG, IServiceScope scopedE ) where T : notnull
            {
                var sing1 = scopedG.ServiceProvider.GetRequiredService<T>();
                scopedE.ServiceProvider.GetService( typeof( T ) ).Should().BeSameAs( sing1 );
                // That is the one ultimately stored in the root container.
                g.GetService( typeof( T ) ).Should().BeSameAs( sing1 ).And.BeSameAs( e.GetService( typeof( T ) ) );
                return sing1;
            }
        }


        // We cannot apply to global singletons what we do for scoped services.
        // If we do, we impact the direct instance registration capability...
        // The undetected edge case (where we cannot throw an exception) is when only one (badly)
        // type mapped singleton is registered... So we can live with that but this clearly
        // shows the limits of the current "Conformant DI".
        //
        // Note: The root cause is that there is a fundamental ambiguity between the registration
        //       of a type mapping and a multiple type mapping.
        //       Adding a "bool IsMultiple" or, better, an explicit "Type? MultiTargetType" on ServiceDescriptor would
        //       be enough to disambiguate the registrations.
        //
        [TestCase( "hidden scoped" )]
        [TestCase( "hidden singleton (undetectable bug)" )]
        [TestCase( "hidden singleton (miraculous good registration order)" )]
        public void when_hybrid_lifetime_multiple_registrations_fails_the_IEnumerable_must_NOT_be_used( string mode )
        {
            var global = new ServiceCollection();
            FakeHost.ConfigureGlobal( global );
            // 
            global.AddSingleton<Sing1>();
            global.AddScoped<Scop1>();

            if( mode == "hidden scoped" )
            {
                global.AddSingleton<IMulti, Sing1>( sp => sp.GetRequiredService<Sing1>() );
                // Invalid registration (ImplementationType is not discoverable).
                global.AddScoped<IMulti>( sp => sp.GetRequiredService<Scop1>() );
            }
            else if( mode == "hidden singleton (undetectable bug)" )
            {
                // Invalid registration (ImplementationType is not discoverable).
                global.AddSingleton<IMulti>( sp => sp.GetRequiredService<Sing1>() );
                global.AddScoped<IMulti, Scop1>( sp => sp.GetRequiredService<Scop1>() );
            }
            else
            {
                // Miraculous good registration order...
                global.AddScoped<IMulti, Scop1>( sp => sp.GetRequiredService<Scop1>() );
                // Invalid registration (ImplementationType is not discoverable).
                global.AddSingleton<IMulti>( sp => sp.GetRequiredService<Sing1>() );
            }

            IEndpointServiceProvider<FakeEndpointDefinition.Data>? e = FakeHost.CreateServiceProvider( TestHelper.Monitor, global, out var g );
            Debug.Assert( e != null );

            using var scopedG = g.CreateScope();
            var ubiq = scopedG.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();
            using var scopedE = e.CreateAsyncScope( new FakeEndpointDefinition.Data( ubiq, TestHelper.Monitor ) );

            // The container works as usual.
            IEnumerable<Sing1> eSing1 = scopedE.ServiceProvider.GetServices<Sing1>();
            eSing1.Single().Should().BeOfType<Sing1>();
            IEnumerable<Scop1> eScop1 = scopedE.ServiceProvider.GetServices<Scop1>();
            eScop1.Single().Should().BeOfType<Scop1>();

            if( mode == "hidden scoped" )
            {
                // Except if the IEnumerable is requested.
                FluentActions.Invoking( () => scopedE.ServiceProvider.GetServices<IMulti>() )
                    .Should().Throw<InvalidOperationException>();
            }
            else
            {
                var m = scopedE.ServiceProvider.GetServices<IMulti>();
                if( mode == "hidden singleton (undetectable bug)" )
                {
                    m.Select( o => o.GetType() ).Should().BeEquivalentTo( new[] { typeof( Scop1 ), typeof( Scop1 ) } );
                }
                else
                {
                    // Miraculous good registration order...
                    m.Select( o => o.GetType() ).Should().BeEquivalentTo( new[] { typeof( Scop1 ), typeof( Sing1 ) } );
                }
            }
        }


        [Test]
        public void ubiquitous_services_test()
        {
            ServiceCollection global = new ServiceCollection();
            FakeHost.ConfigureGlobal( global );

            IEndpointServiceProvider<FakeEndpointDefinition.Data>? e = FakeHost.CreateServiceProvider( TestHelper.Monitor, global, out var g );
            Debug.Assert( e != null && g != null );

            using var scopedG = g.CreateScope();
            // From the global, obtains a EndpointUbiquitousInfo.
            var ubiq = scopedG.ServiceProvider.GetRequiredService<EndpointUbiquitousInfo>();

            using var scopedE = e.CreateAsyncScope( new FakeEndpointDefinition.Data( ubiq, TestHelper.Monitor ) );

            (A A, B B, IEnumerable<A> MultiA, Scoped S) fromE;
            (A A, B B, IEnumerable<A> MultiA, Scoped S) fromG;
            fromE = ResolveFrom( scopedE.ServiceProvider );
            fromG = ResolveFrom( scopedG.ServiceProvider );

            fromE.A.Should().BeSameAs( fromG.A );
            fromE.B.Should().BeSameAs( fromG.B );
            fromE.MultiA.Should().NotBeSameAs( fromG.MultiA, "Unfortunately... But its content is okay, and anyway, see below: " +
                                                             "instances are always different for resolved IEnumerable<>." );
            fromE.MultiA.Select( a => a.Name ).Should().BeEquivalentTo( new[] { "A instance", "A instance by factory" } );
            fromE.MultiA.ElementAt( 0 ).Should().BeSameAs( fromG.MultiA.ElementAt( 0 ) );
            fromE.MultiA.ElementAt( 1 ).Should().BeSameAs( fromG.MultiA.ElementAt( 1 ) );

            fromE.S.Should().NotBeSameAs( fromG.S, "Scoped are obviously different instances." );
            fromE.S.B.Should().BeSameAs( fromG.S.B ).And.BeSameAs( fromG.B );
            fromE.S.MultipleA.Should().BeSameAs( fromE.MultiA );
            fromG.S.MultipleA.Should().NotBeSameAs( fromG.MultiA, "Each resolution of IEnumerable<> leads to a different instance." );

            // When we ask for IEnumerable<B>, we always have a different (single) enumeration.
            var gB = scopedG.ServiceProvider.GetRequiredService<IEnumerable<B>>();
            gB.Should().NotBeSameAs( scopedG.ServiceProvider.GetRequiredService<IEnumerable<B>>() );
            var eB = scopedG.ServiceProvider.GetRequiredService<IEnumerable<B>>();
            eB.Should().NotBeSameAs( scopedE.ServiceProvider.GetRequiredService<IEnumerable<B>>() );

            gB.Should().HaveCount( 1 ).And.Contain( new[] { e.GetRequiredService<B>() } );
            eB.Should().HaveCount( 1 ).And.Contain( new[] { e.GetRequiredService<B>() } );

            //
            using var scopedG2 = g.CreateScope();
            using var scopedE2 = e.CreateAsyncScope( new FakeEndpointDefinition.Data( ubiq, TestHelper.Monitor ) );

            var fromE2 = ResolveFrom( scopedE2.ServiceProvider );
            var fromG2 = ResolveFrom( scopedG2.ServiceProvider );

            fromG2.A.Should().BeSameAs( fromG.A );
            fromG2.B.Should().BeSameAs( fromG.B );
            fromG2.S.Should().NotBeSameAs( fromG.S );

            fromE2.A.Should().BeSameAs( fromE.A ).And.BeSameAs( fromG.A );
            fromE2.B.Should().BeSameAs( fromE.B ).And.BeSameAs( fromG.B );
            fromE2.S.Should().NotBeSameAs( fromE.S );

            static (A A, B B, IEnumerable<A> MultiA, Scoped S) ResolveFrom( IServiceProvider sp )
            {
                return (sp.GetRequiredService<A>(), sp.GetRequiredService<B>(), sp.GetRequiredService<IEnumerable<A>>(), sp.GetRequiredService<Scoped>());
            }
        }

    }
}
