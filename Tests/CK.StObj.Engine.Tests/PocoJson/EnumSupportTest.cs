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

            object? Result { get; set; }
        }


        [Test]
        public void enum_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Working = Code.Pending; o.Result = "CodeGen!"; } );
            var o2 = (ITest)JsonTestHelper.Roundtrip( directory, o );

            Debug.Assert( o2 != null );
            o2.Working.Should().Be( Code.Pending );
            o2.Result.Should().Be( "CodeGen!" );
        }



    }
}
