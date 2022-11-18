using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
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

    }
}
