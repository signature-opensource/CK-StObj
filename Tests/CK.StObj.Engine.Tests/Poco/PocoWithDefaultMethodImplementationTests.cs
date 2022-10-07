using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoWithDefaultMethodImplementationTests
    {
        [CKTypeDefiner]
        public interface IRootDefiner : IPoco
        {
            List<string> Lines { get; }

            int LineCount => Lines.Count;
        }

        public interface IActualRoot : IRootDefiner
        {
            List<string> Rows { get; }

            int RowCount
            {
                get => Rows.Count;
                set => Rows.RemoveRange( value, Rows.Count - value );

            }

            void Clear()
            {
                Lines.Clear();
                Rows.Clear();
            }
        }

        [Test]
        public void default_implementation_methods_work()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IActualRoot ) );
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
