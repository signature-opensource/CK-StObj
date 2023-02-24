using CK.Core;
using NUnit.Framework;
using System.Collections.Generic;
using static CK.StObj.Engine.Tests.Poco.PocoGenericTests;
using static CK.StObj.Engine.Tests.Poco.PocoMaskingPropertiesTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoMaskingPropertiesTests
    {
        [CKTypeDefiner]
        public interface IRootDefiner : IPoco
        {
            IReadOnlyList<ISubDefiner> Lines { get; }
        }

        [CKTypeDefiner]
        public interface ISubDefiner : IPoco
        {
        }

        public interface IActualRootA : IRootDefiner
        {
            new List<IActualSubA> Lines { get; }
        }

        public interface IActualSubA : ISubDefiner
        {
        }

        [Test]
        public void masking_properties_from_definer_is_NOT_CURRENTLY_possible()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IActualRootA ), typeof( IActualSubA ) );
            TestHelper.GetFailedResult( c );
            //using var s = TestHelper.CreateAutomaticServices( c ).Services;
            //var d = s.GetRequiredService<PocoDirectory>();
            //var fA = d.Find( "CK.StObj.Engine.Tests.Poco.IActualRootA" );
            //Debug.Assert( fA != null ); 
            //var a = (IActualRootA)fA.Create();
            //a.Lines.Should().BeOfType<IActualSubA>();
        }

    }
}
