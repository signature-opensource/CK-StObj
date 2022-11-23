using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public class EnumSupportTest
    {
        [ExternalName("WorkingCode")]
        public enum Code
        {
            None,
            Working,
            Pending
        }

        public interface ITest : IPoco
        {
            Code Working { get; set; }

            Code? NullableWorking { get; set; }

            object? Result { get; set; }
        }

        [Test]
        public void enum_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( ITest ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Working = Code.Pending; o.NullableWorking = Code.Working; o.Result = Code.None; } );
            o.ToString().Should().Be( @"{""Working"":2,""NullableWorking"":1,""Result"":[""WorkingCode"",0]}" );
        }

        [Test]
        public void enum_serialization_roundtrip()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( PocoJsonImportSupport ), typeof( ITest ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Working = Code.Pending; o.NullableWorking = Code.Working; o.Result = Code.None; } );
            var o2 = JsonTestHelper.Roundtrip( directory, o );

            Debug.Assert( o2 != null );
            o2.Working.Should().Be( Code.Pending );
            o2.NullableWorking.Should().Be( Code.Working );
            o2.Result.Should().Be( Code.None );

            o.NullableWorking = null;
            o2 = JsonTestHelper.Roundtrip( directory, o );
            o2.Working.Should().Be( Code.Pending );
            o2.NullableWorking.Should().BeNull();
            o2.Result.Should().Be( Code.None );
        }

        [ExternalName("ULCode")]
        public enum CodeOnULong : ulong
        {
            None,
            Working = long.MaxValue,
            Pending = ulong.MaxValue
        }

        public interface IWithULong : IPoco
        {
            CodeOnULong Code { get; set; }

            object? Result { get; set; }
        }

        [Test]
        public void enum_serialization_with_ulong_underlying_type()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IWithULong ) ); ;
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithULong>>();
            var o = f.Create( o => { o.Code = CodeOnULong.None; o.Result = CodeOnULong.None; } );
            o.ToString().Should().Be( @"{""Code"":""0"",""Result"":[""ULCode"",""0""]}" );

            o.Code = CodeOnULong.Working;
            o.Result = o.Code;
            o.ToString().Should().Be( @"{""Code"":""9223372036854775807"",""Result"":[""ULCode"",""9223372036854775807""]}" );

            o.Code = CodeOnULong.Pending;
            o.Result = o.Code;
            o.ToString().Should().Be( @"{""Code"":""18446744073709551615"",""Result"":[""ULCode"",""18446744073709551615""]}" );
        }

    }
}
