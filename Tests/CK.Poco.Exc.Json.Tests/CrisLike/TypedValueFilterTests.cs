using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Poco.Exc.Json.Tests.CrisLike;

/// <summary>
/// A Poco that is serializable but not exchangeable: it is in the "AllSerializable" type set,
/// not in the "AllExchangeable" one.
/// </summary>
[NotExchangeable]
public interface ILocalOnlyCommand : IPoco
{
    string Name { get; set; }
}

public interface IAnyHolder : IPoco
{
    object? Any { get; set; }
}

[TestFixture]
public class TypedValueFilterTests
{
    static string Write( PocoDirectory directory, IPoco poco )
    {
        var buffer = new ArrayBufferWriter<byte>();
        directory.WriteJson( buffer, poco, withType: true, new PocoJsonExportOptions { TypeFilterName = "AllSerializable" } ).ShouldBeTrue();
        return Encoding.UTF8.GetString( buffer.WrittenSpan );
    }

    [Test]
    public async Task the_type_filter_applies_to_the_typed_values_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( ILocalOnlyCommand ), typeof( IAnyHolder ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var directory = auto.Services.GetRequiredService<PocoDirectory>();

        var local = directory.Create<ILocalOnlyCommand>( c => c.Name = "local" );
        var allSerializable = new PocoJsonImportOptions { TypeFilterName = "AllSerializable" };

        // The root of the document is a typed value.
        var json = Write( directory, local );
        directory.ReadJson( json, allSerializable ).ShouldBeAssignableTo<ILocalOnlyCommand>().Name.ShouldBe( "local" );
        Util.Invokable( () => directory.ReadJson( json, PocoJsonImportOptions.Default ) )
            .ShouldThrow<JsonException>()
            .Message.ShouldContain( "is not allowed by the 'AllExchangeable' type filter." );

        // A polymorphic field holds a typed value.
        var holder = directory.Create<IAnyHolder>( h => h.Any = local );
        json = Write( directory, holder );
        directory.ReadJson( json, allSerializable ).ShouldBeAssignableTo<IAnyHolder>().Any.ShouldBeAssignableTo<ILocalOnlyCommand>();
        Util.Invokable( () => directory.ReadJson( json, PocoJsonImportOptions.Default ) )
            .ShouldThrow<JsonException>()
            .Message.ShouldContain( "is not allowed by the 'AllExchangeable' type filter." );
    }
}
