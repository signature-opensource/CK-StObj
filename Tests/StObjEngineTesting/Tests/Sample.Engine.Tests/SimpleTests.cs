using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sample.Model;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace Sample.Engine.Tests;

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
    public async Task StupidCodeAttribute_works_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ThingService ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var thing = auto.Services.GetRequiredService<ThingService>();
        thing.GetValue( "ab" ).ShouldBe( 2 );
        thing.GetAnotherValue( "abc" ).ShouldBe( 9 );
        using( TestHelper.Monitor.CollectTexts( out var logs ) )
        {
            thing.SaySomething( TestHelper.Monitor ).ShouldBe( "Yes!" );
            logs.ShouldContain( "Hello World!" );
        }
    }

}
