using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;


namespace CK.StObj.Engine.Tests.Poco
{

    [TestFixture]
    public class PocoTypeSetTests
    {
        public interface IEmptyPoco : IPoco
        {
        }

        public record struct NamedRecord( Guid UId, string Name );

        public interface IPoco1 : IPoco
        {
            string Name { get; set; }
            int Age { get; set; }
            bool IsAdmin { get; set; }
            IList<IEmptyPoco> ListEmptyPoco { get; }
            IList<NamedRecord> ListNamedRecord { get; }
        }

        [Test]
        public void basic_set_tests()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEmptyPoco ), typeof( IPoco1 ) );
            var ts = TestHelper.GetSuccessfulResult( c ).PocoTypeSystemBuilder.Lock();
            var empty = ts.FindByType( typeof( IEmptyPoco ) );
            var poco1 = ts.FindByType( typeof( IPoco1 ) );
            var guid = ts.FindByType( typeof( Guid ) );
            var str = ts.FindByType( typeof( string ) );
            var iListEmptyPoco = ts.AllNonNullableTypes.Single( t => t.CSharpName == "IList<CK.StObj.Engine.Tests.Poco.PocoTypeSetTests.IEmptyPoco>" );
            var listEmptyPoco = ts.FindByType( typeof( List<IEmptyPoco> ) );
            var namedRec = ts.FindByType( typeof( NamedRecord ) );
            var iListNamedRec = ts.AllNonNullableTypes.Single( t => t.CSharpName == "IList<CK.StObj.Engine.Tests.Poco.PocoTypeSetTests.NamedRecord>" );
            var listNamedRec = ts.FindByType( typeof( List<NamedRecord> ) );
            Throw.DebugAssert( empty != null && poco1 != null && guid != null && str != null
                               && iListEmptyPoco != null && listEmptyPoco != null
                               && namedRec != null && iListNamedRec != null && listNamedRec != null
                               && iListNamedRec != listNamedRec );

            ts.SetManager.All.AllowEmptyRecords.Should().BeTrue();
            ts.SetManager.All.AllowEmptyPocos.Should().BeTrue();
            new[] { empty, poco1, guid, str, listEmptyPoco, iListEmptyPoco, namedRec, iListNamedRec, listNamedRec }.Should().BeSubsetOf( ts.SetManager.All.NonNullableTypes );
            ts.SetManager.AllSerializable.Should().BeSameAs( ts.SetManager.All );
            ts.SetManager.AllExchangeable.Should().BeSameAs( ts.SetManager.All );

            var s1 = ts.SetManager.All.Exclude( new[] { namedRec } );
            s1.NonNullableTypes.Should().NotContain( namedRec )
                                    .And.NotContain( iListNamedRec )
                                    .And.NotContain( listNamedRec );
            new[] { empty, poco1, guid, str, listEmptyPoco, iListEmptyPoco }.Should().BeSubsetOf( ts.SetManager.All.NonNullableTypes );

            var s2 = s1.Include( new[] { namedRec } );
            s2.NonNullableTypes.Should().BeEquivalentTo( ts.SetManager.All.NonNullableTypes );

            s2.Include( ts.SetManager.All.NonNullableTypes ).Should().BeSameAs( s2 );
            s2.Exclude( Enumerable.Empty<IPocoType>() ).Should().BeSameAs( s2 );

            var sNoEmptyPoco = ts.SetManager.All.ExcludeEmptyPocos();
            sNoEmptyPoco.NonNullableTypes.Should().NotContain( empty )
                                              .And.NotContain( iListEmptyPoco )
                                              .And.NotContain( listEmptyPoco );
            new[] { poco1, guid, str, namedRec, iListNamedRec, listNamedRec }.Should().BeSubsetOf( sNoEmptyPoco.NonNullableTypes );

            sNoEmptyPoco.Include( new[] { empty } ).Should().BeSameAs( sNoEmptyPoco );
        }


    }
}

