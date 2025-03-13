using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0051 // Remove unused private members

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class DefaultValueTests
{
    public interface IThing : IPoco
    {
        [DefaultValue( 3712 )]
        int Power { get; set; }
    }

    public interface IThingHolder : IPoco
    {
        IThing Value { get; }
    }

    [Test]
    public async Task default_values_on_simple_field_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IThing ), typeof( IThingHolder ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var h = auto.Services.GetRequiredService<IPocoFactory<IThingHolder>>().Create();
        h.Value.ShouldNotBeNull();
        h.Value.Power.ShouldBe( 3712 );
    }

    public enum TypeDefaultIsUnsignedMinimalValue
    {
        NotTheDefault = -1,
        NotTheDefault2 = -89794,
        NotTheDefault3 = 43,
        NotTheDefault4 = 6546,
        ThisIsTheDefault = 42,
    }

    [Test]
    public void default_values_on_enum_is_the_Unsigned_minimal_value()
    {
        var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );
        var t = ts.RegisterOblivious( TestHelper.Monitor, typeof( TypeDefaultIsUnsignedMinimalValue ) );

        Debug.Assert( t != null && t.DefaultValueInfo.DefaultValue != null );
        t.DefaultValueInfo.DefaultValue.SimpleValue.ShouldBe( TypeDefaultIsUnsignedMinimalValue.ThisIsTheDefault );
        t.DefaultValueInfo.DefaultValue.ValueCSharpSource.ShouldBe( "CK.StObj.Engine.Tests.Poco.DefaultValueTests.TypeDefaultIsUnsignedMinimalValue.ThisIsTheDefault" );

        var tE = (IEnumPocoType)t;
        tE.DefaultValueName.ShouldBe( "ThisIsTheDefault" );
    }

}
