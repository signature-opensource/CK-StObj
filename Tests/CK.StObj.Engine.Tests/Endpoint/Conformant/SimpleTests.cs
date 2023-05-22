using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.StObj.Engine.Tests.Endpoint.DITests
{
    [TestFixture]
    public class SimpleTests
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
            public B( string name ) : base(name ) { }
        }

        [Test]
        public void you_can_do_whatever_you_want_demo()
        {
            var c = new ServiceCollection();
            c.AddSingleton<A>( new A( "I'm the A." ) );
            c.AddSingleton<B>( new B( "I'm the B." ) );
            c.AddSingleton<IA>( sp => sp.GetRequiredService<B>() );
            c.AddSingleton<IB>( sp => sp.GetRequiredService<B>() );

            var s = c.BuildServiceProvider();
            s.GetRequiredService<A>().Name.Should().Be( "I'm the A." );
            s.GetRequiredService<B>().Name.Should().Be( "I'm the B." );

            s.GetRequiredService<IA>().Name.Should().Be( "I'm the B." );
            s.GetRequiredService<IB>().Name.Should().Be( "I'm the B." );
        }


        class GlobalHook
        {
            [AllowNull]
            public IServiceProvider Global { get; set; }
        }

        class Scoped
        {
            public Scoped( B b, IEnumerable<A> multipleA )
            {
                B = b;
                MultipleA = multipleA;
            }

            public B B { get; }

            public IEnumerable<A> MultipleA { get; }
        }

        [Test]
        public void relay_to_global_DI_explained()
        {
            ServiceCollection global = new ServiceCollection();
            // B is "normal".
            global.AddSingleton<B>( new B( "B instance" ) );
            global.AddSingleton<IB>( sp => sp.GetRequiredService<B>() );
            // A is registered twice.
            global.AddSingleton( new A( "A instance" ) );
            global.AddSingleton( sp => new A( "A instance by factory" ) );
            // This scoped must be bound to the B and the two A instance.
            global.AddScoped<Scoped>();

            var singletons = new Dictionary<Type,bool>();
            ServiceCollection endpoint = new ServiceCollection();
            endpoint.AddSingleton( new GlobalHook() );
            foreach( var d in global )
            {
                if( d.Lifetime == ServiceLifetime.Singleton )
                {
                    if( singletons.TryGetValue( d.ServiceType, out var multi ) )
                    {
                        singletons[d.ServiceType] = true;
                    }
                    else
                    {
                        singletons.Add( d.ServiceType, false );
                    }
                }
                else
                {
                    endpoint.Add( d );
                }
            }
            foreach( var (type,multi) in singletons )
            {
                endpoint.AddSingleton( type, sp => sp.GetRequiredService<GlobalHook>().Global.GetService( type )! );
                if( multi )
                {
                    var eType = typeof( IEnumerable<> ).MakeGenericType( type );
                    endpoint.AddSingleton( eType, sp => sp.GetRequiredService<GlobalHook>().Global.GetService( eType )! );
                }
            }
            var g = global.BuildServiceProvider();
            var e = endpoint.BuildServiceProvider();
            e.GetRequiredService<GlobalHook>().Global = g;

            using var scopedG = g.CreateScope();
            using var scopedE = e.CreateScope();

            (A A, B B, IEnumerable<A> MultiA, Scoped S) fromE;
            (A A, B B, IEnumerable<A> MultiA, Scoped S) fromG;
            fromE = ResolveFrom( scopedE.ServiceProvider );
            fromG = ResolveFrom( scopedG.ServiceProvider );

            fromE.A.Should().BeSameAs( fromG.A );
            fromE.B.Should().BeSameAs( fromG.B );
            fromE.MultiA.Should().NotBeSameAs( fromG.MultiA, "Unfortunately... But its content is okay, and anyway, see below, " +
                                                             "instances are always different for resolved IEnumerable<>." );
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
        }

        (A A,B B, IEnumerable<A> MultiA, Scoped S) ResolveFrom( IServiceProvider sp )
        {
            return (sp.GetRequiredService<A>(), sp.GetRequiredService<B>(), sp.GetRequiredService<IEnumerable<A>>(), sp.GetRequiredService<Scoped>() );
        }

        interface IMulti { }

        // This one is registered as 
        class S1 : IMulti { }

        class S2 : IMulti { }
    }
}
