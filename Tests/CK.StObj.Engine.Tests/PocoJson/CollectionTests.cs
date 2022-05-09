using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class CollectionTests
    {
        public interface IListOfList : IPoco
        {
            List<List<int>> List { get; }
        }

        [Test]
        public void list_of_list_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IListOfList ) );
            var s = TestHelper.GetAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IListOfList>>();
            var oD = f.Create( o => { o.List.Add( new List<int> { 1, 2 } ); } );
            var oD2 = JsonTestHelper.Roundtrip( directory, oD );
            oD2.List[0].Should().HaveCount( 2 );
        }

    }
}
