using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class PocoUnionTypeTestsCanBeExtended
{


    public interface IPoco1 : IPoco
    {
        [UnionType( CanBeExtended = true )]
        object Thing { get; set; }

        class UnionTypes
        {
            public (decimal, int) Thing { get; }
        }
    }

    public interface IPoco2 : IPoco1
    {
        [UnionType( CanBeExtended = true )]
        new object Thing { get; set; }

        [UnionType( CanBeExtended = true )]
        object? AnotherThing { get; set; }

        new class UnionTypes
        {
            public (string, List<string>) Thing { get; }

            public (int, double) AnotherThing { get; }
        }
    }

    public interface IPoco2Bis : IPoco1
    {
        [UnionType( CanBeExtended = true )]
        object? AnotherThing { get; set; }

        new class UnionTypes
        {
            public (string, List<string?>) AnotherThing { get; }
        }
    }

    [Test]
    public async Task Union_types_can_be_extendable_as_long_as_CanBeExtended_is_specified_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IPoco1 ), typeof( IPoco2 ), typeof( IPoco2Bis ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var p = auto.Services.GetRequiredService<IPocoFactory<IPoco2>>().Create();

        // Thing allows int, decimal, string and List<string> (not nullable!)
        p.Thing = 34;
        p.Thing.ShouldBe( 34 );
        p.Thing = (decimal)555;
        p.Thing.ShouldBe( (decimal)555 );
        p.Thing = "It works!";
        p.Thing.ShouldBe( "It works!" );

        Util.Invokable( () => p.Thing = null! ).ShouldThrow<ArgumentException>( "Null is forbidden." );
        Util.Invokable( () => p.Thing = new Dictionary<string, object>() ).ShouldThrow<ArgumentException>( "Not an allowed type." );

        // AnotherThing allows int, double?, string? and List<string?>?
        p.AnotherThing = 34;
        p.AnotherThing.ShouldBe( 34 );
        p.AnotherThing = 0.04e-5;
        p.AnotherThing = null;
        p.AnotherThing.ShouldBeNull();

        Util.Invokable( () => p.AnotherThing = (decimal)555 ).ShouldThrow<ArgumentException>( "Not an allowed type." );
        Util.Invokable( () => p.AnotherThing = new Dictionary<string, object>() ).ShouldThrow<ArgumentException>( "Not an allowed type." );
    }


}
