using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using static CK.Poco.Exc.Json.Tests.BasicTypeTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public class RecordTests
    {
        public record struct Thing( string Name, int Count );

        public interface IWithRecord : IPoco
        {
            ref Thing Hop { get; }
        }

        [Test]
        public void simple_tuple_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( CommonPocoJsonSupport ), typeof( IWithRecord ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithRecord>>();
            var o = f.Create( o =>
            {
                o.Hop.Name = "Albert";
                o.Hop.Count = 3712;
            } );
            o.ToString().Should().Be( @"{""Hop"":{""Name"":""Albert"",""Count"":3712}}" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( new Thing( "Albert", 3712 ) );
        }

        public interface IWithNullableRecord : IPoco
        {
            ref Thing? Hop { get; }
        }

        [Test]
        public void simple_nullable_tuple_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add(typeof( CommonPocoJsonSupport ), typeof( IWithNullableRecord ));
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithNullableRecord>>();
            var o = f.Create( o => { o.Hop = new Thing("Hip", 42); } );
            o.ToString().Should().Be( @"{""Hop"":{""Name"":""Hip"",""Count"":42}}" );

            var o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Hop.Should().Be( new Thing( "Hip", 42 ) );

            o.Hop = null;
            o.ToString().Should().Be( @"{""Hop"":null}" );

            var o3 = JsonTestHelper.Roundtrip( directory, o );
            o3.Hop.Should().BeNull();
        }

    }
}
