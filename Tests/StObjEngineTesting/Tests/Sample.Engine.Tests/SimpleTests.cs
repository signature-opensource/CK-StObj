using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sample.Model;

using static CK.Testing.StObjEngineTestHelper;

namespace Sample.Engine.Tests
{
    [TestFixture]
    public class SimpleTests
    {

        public abstract class ThingService : IAutoService
        {
            [StupidCode( "s.Length", IsLambda = true )]
            public abstract int GetValue( string s );

            [StupidCode( "return s.Length * 3;" )]
            public abstract int GetAnotherValue( string s );

            [StupidCode( "base.SaySomething( monitor, messageFormat ); return \"Yes!\";" )]
            public virtual string SaySomething( IActivityMonitor monitor, string messageFormat = "Hello {0}!" )
            {
                var msg = string.Format( messageFormat, "World" );
                monitor.Info( msg );
                return msg;
            }
        }

        [Test]
        public void StupidCodeAttribute_works()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ThingService ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var thing = s.GetRequiredService<ThingService>();
            thing.GetValue( "ab" ).Should().Be( 2 );
            thing.GetAnotherValue( "abc" ).Should().Be( 9 );
            thing.SaySomething( TestHelper.Monitor ).Should().Be( "Yes!" );
        }

    }
}