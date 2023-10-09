using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public partial class WriteReadAnyTests
    {
        [ExternalName("SomeType")]
        public interface ISomeTypes : IPoco
        {
            int[] Value { get; set; }
            IList<ISomeTypes> Friends { get; }
        }

        [Test]
        public void ReadAnyJson_tests()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( ISomeTypes ) ); ;
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = services.GetRequiredService<PocoDirectory>();

            directory.ReadAnyJson( "null" ).Should().BeNull();
            directory.ReadAnyJson( "3712" ).Should().Be( 3712.0 );
            directory.ReadAnyJson( "true" ).Should().Be( true );
            directory.ReadAnyJson( "false" ).Should().Be( false );
            var def = directory.ReadAnyJson( """["SomeType",{}]""" );
            Throw.DebugAssert( def != null );
            var tDef = (ISomeTypes)def;
            tDef.Value.Should().NotBeNull().And.BeEmpty();
            tDef.Friends.Should().BeEmpty();

            var withFriends = directory.ReadAnyJson( """
                                                     ["SomeType",{
                                                       "Value": [1],
                                                       "Friends": [
                                                         {
                                                         },
                                                         {
                                                            "Value": [1,2]
                                                         },
                                                         {
                                                            "Value": [1,2,3],
                                                            "Friends": [{"Value": [1,2,3,4]},{"Value": [1,2,3,4,5]}]
                                                         }
                                                       ]
                                                     }]
                                                     """ );
            Throw.DebugAssert( withFriends != null );
            var tWithFriends = (ISomeTypes)withFriends;
            tWithFriends.Value.Should().HaveCount( 1 ).And.Contain( 1 );
            tWithFriends.Friends.Should().HaveCount( 3 );
            tWithFriends.Friends[0].Should().BeEquivalentTo( tDef );
            tWithFriends.Friends[1].Value.Should().HaveCount( 2 ).And.Contain( new[] { 1, 2 } );
            tWithFriends.Friends[2].Value.Should().HaveCount( 3 ).And.Contain( new[] { 1, 2, 3 } );
            tWithFriends.Friends[2].Friends[0].Value.Should().HaveCount( 4 ).And.Contain( new[] { 1, 2, 3, 4 } );
            tWithFriends.Friends[2].Friends[1].Value.Should().HaveCount( 5 ).And.Contain( new[] { 1, 2, 3, 4, 5 } );
        }

    }
}
