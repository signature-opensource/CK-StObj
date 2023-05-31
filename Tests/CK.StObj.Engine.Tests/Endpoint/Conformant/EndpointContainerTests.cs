using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint.Conformant
{
    [TestFixture]
    public partial class EndpointContainerTests
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

    }
}
