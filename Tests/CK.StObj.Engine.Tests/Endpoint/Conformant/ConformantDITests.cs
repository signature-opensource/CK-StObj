using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant;

[TestFixture]
public partial class ConformantDITests
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

    [Test]
    public void right_way_to_inject_a_monitor()
    {
        // With the ActivityMonitor type (that is useless).
        {
            var services = new ServiceCollection();
            services.AddScoped<ActivityMonitor>(); // Useless!
            services.AddScoped<IActivityMonitor>( sp => sp.GetRequiredService<ActivityMonitor>() );
            services.AddScoped( sp => sp.GetRequiredService<ActivityMonitor>().ParallelLogger );
            CheckValid( services );
            // This is a "multiple" compliant registration but we don't care since the IActivityMonitor
            // must fundamentally be a shared instance!
            IsMultipleCompliant( services, typeof( IActivityMonitor ) ).Should().BeTrue();
            IsMultipleCompliant( services, typeof( IParallelLogger ) ).Should().BeTrue();
        }
        // Without the ActivityMonitor type exposed.
        {
            var services = new ServiceCollection();
            // This is the right way!
            services.AddScoped<IActivityMonitor, ActivityMonitor>();
            services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );

            CheckValid( services );
            // This is also "multiple" compliant.
            IsMultipleCompliant( services, typeof( IActivityMonitor ) ).Should().BeTrue();
            IsMultipleCompliant( services, typeof( IParallelLogger ) ).Should().BeTrue();
        }

        static void CheckValid( ServiceCollection services )
        {
            ServiceProvider appServices = services.BuildServiceProvider();
            IServiceScope scoped = appServices.CreateScope();
            var sp = scoped.ServiceProvider;
            var monitor = sp.GetRequiredService<IActivityMonitor>();
            var parallel = sp.GetRequiredService<IParallelLogger>();
            parallel.Should().BeSameAs( monitor.ParallelLogger );
        }

        bool IsMultipleCompliant( ServiceCollection services, Type type )
        {
            // To support IEnumerable<T> from endpoint containers, the descriptors must
            // capture the mapped type correctly: if a factory is used it must be a typed one,
            // not a factory that returns "object".
            // This is not the only constraint: the mapped types of multiple registrations must
            // be different or you'll end up with duplicates and out of reach instances in the
            // enumeration.
            var impl = new List<Type>();
            foreach( var d in services.Where( d => d.ServiceType == type ) )
            {
                var target = GetImplementationType( d );
                if( target == typeof( object ) )
                {
                    // Bad registration. We won't be able to get the target instance.
                    return false;
                }
                if( impl.Contains( target ) )
                {
                    // Bad registrations: the multiple type 't' is mapped twice to the
                    // same implementation type: the last registered will be a duplicate
                    // and we have lost one.
                    return false;
                }
            }
            // Nothing prevents a IEnumerable<> to be synthesized for 't'.
            // (It's not totally true: we have ignored the lifetime here. Things get a little bit more
            // complex for hybrid lifetime multiples).
            return true;
        }

        // This is how the implementation type is guessed by the ServiceProvider implementation.
        // We need this "guess" to handle IEnumerable<T> from endpoint containers.
        // If the returned type is "object", we are stuck...
        static Type GetImplementationType( ServiceDescriptor d )
        {
            if( d.ImplementationType != null )
            {
                return d.ImplementationType;
            }
            else if( d.ImplementationInstance != null )
            {
                // This is for singleton only.
                Debug.Assert( d.Lifetime == ServiceLifetime.Singleton );
                return d.ImplementationInstance.GetType();
            }
            Debug.Assert( d.ImplementationFactory != null );
            Type[]? typeArguments = d.ImplementationFactory.GetType().GenericTypeArguments;
            return typeArguments[1];
        }

    }
}
