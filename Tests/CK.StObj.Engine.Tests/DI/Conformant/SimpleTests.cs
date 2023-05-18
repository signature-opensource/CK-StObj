using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace CK.StObj.Engine.Tests.DI.Conformant
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

    }
}
