using CK.Core;
using CK.Setup;
using CommunityToolkit.HighPerformance;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static CK.Poco.Exc.Json.Tests.CollectionTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public partial class CollectionTests
    {
        [ExternalName( "IWithArray" )]
        public interface IWithArray : IPoco
        {
            int[] ArrayOfInt { get; set; }

            [RegisterPocoType( typeof( string ) )]
            object[] ArrayOfObject { get; set; }

            long[][] ArrayOfArrayOfLong { get; set; }

            IWithArray[]? ArrayOfMe { get; set; }

            object? Result { get; set; }
        }

        [Test]
        public void arrays_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithArray ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithArray>>();
            var o1 = f.Create( o => o.Result = "This-is-o1!" );
            var o2 = f.Create( o =>
            {
                o.ArrayOfInt = new int[] { 1, 2 };
                o.ArrayOfObject = new object[] { 1, "Hello" };
                o.ArrayOfArrayOfLong = new long[][] { new long[] { 1, 2 }, new long[] { 3, 4, 5 } };
                o.ArrayOfMe = new[] { o1 };
                o.Result = new object[] { o.ArrayOfInt, o.ArrayOfObject, o.ArrayOfArrayOfLong };
            } );
            o2.ToString().Should().Be( @"
                {
                    ""ArrayOfInt"": [1,2],
                    ""ArrayOfObject"":
                        [
                            [""int"",1],
                            [""string"",""Hello""]
                        ],
                    ""ArrayOfArrayOfLong"":
                        [
                            [""1"",""2""],
                            [""3"",""4"",""5""]
                        ],
                    ""ArrayOfMe"":
                        [
                            {
                                ""ArrayOfInt"": [],
                                ""ArrayOfObject"": [],
                                ""ArrayOfArrayOfLong"": [],
                                ""ArrayOfMe"": null,
                                ""Result"": [""string"",""This-is-o1!""]
                            }
                        ],
                    ""Result"": [""A(object)"",
                                    [
                                        [""A(int)"",[1,2]],
                                        [""A(object)"",
                                            [
                                                [""int"",1],
                                                [""string"",""Hello""]
                                            ]
                                        ],
                                        [""A(A(long))"",
                                            [
                                                [""1"",""2""],
                                                [""3"",""4"",""5""]
                                            ]
                                        ]
                                    ]
                                ]
                }"
                .Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
        }

        public interface IWithLists : IPoco
        {
            IList<object> ListOfList { get; }

            IList<int> CovariantListImpl { get; }

            IList<int?> CovariantListNullableImpl { get; }

            [RegisterPocoType( typeof(object[]) ) ]
            object? Result { get; set; }
        }

        [Test]
        public void lists_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithLists ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithLists>>();
            var oL = f.Create( o =>
            {
                o.ListOfList.Add( new List<int> { 1, 2 } );
                o.ListOfList.Add( new List<int> { 3, 4, 5 } );
                o.CovariantListImpl.Add( 42 );
                o.CovariantListImpl.Add( 3712 );
                o.CovariantListNullableImpl.Add( null );
                o.CovariantListNullableImpl.Add( 0 );
                o.CovariantListNullableImpl.Add( null );
                o.Result = new object[] { o.ListOfList, o.CovariantListImpl, o.CovariantListNullableImpl };
            } );
            CheckToString( oL );

            var oL2 = JsonTestHelper.Roundtrip( directory, oL );
            CheckToString( oL2 );

            static void CheckToString( IWithLists oD )
            {
                oD.ToString().Should().Be( @"
                {
                    ""ListOfList"": [[""L(int)"",[1,2]],[""L(int)"",[3,4,5]]],
                    ""CovariantListImpl"": [42,3712],
                    ""CovariantListNullableImpl"": [null,0,null],
                    ""Result"": [""A(object)"",
                                    [
                                        [""L(object)"",[[""L(int)"",[1,2]],[""L(int)"",[3,4,5]]]],
                                        [""L(int)"",[42,3712]],
                                        [""L(int?)"",[null,0,null]]
                                    ]
                                ]
                }".Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
            }
        }

        public interface IWithSets : IPoco
        {
            ISet<object> SetOfSet { get; }

            ISet<int> CovariantSetImpl { get; }

            ISet<int?> CovariantSetNullableImpl { get; }
        }

        [Test]
        public void sets_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithSets ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithSets>>();
            var oS = f.Create( o =>
            {
                o.SetOfSet.Add( new HashSet<int> { 1, 2 } );
                o.SetOfSet.Add( new HashSet<int> { 3, 4, 5 } );
                o.CovariantSetImpl.Add( 42 );
                o.CovariantSetImpl.Add( 3712 );
                o.CovariantSetNullableImpl.Add( null );
                o.CovariantSetNullableImpl.Add( 0 );
                o.CovariantSetNullableImpl.Add( 1 );
            } );

            CheckToString( oS );

            var oS2 = JsonTestHelper.Roundtrip( directory, oS );
            CheckToString( oS2 );

            static void CheckToString( IWithSets o )
            {
                o.ToString().Should().Be( @"
                {
                    ""SetOfSet"":[[""S(int)"",[1,2]],[""S(int)"",[3,4,5]]],
                    ""CovariantSetImpl"":[42,3712],
                    ""CovariantSetNullableImpl"":[null,0,1]
                }"
                .Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
            }
        }

        public interface IWithDictionaries : IPoco
        {
            [RegisterPocoType( typeof( long ) )]
            [RegisterPocoType( typeof( string ) )]
            IDictionary<object, object> DicOfDic { get; }

            IDictionary<int, int> CovariantDicImpl { get; }

            IDictionary<int, bool?> CovariantDicNullableImpl { get; }
        }

        [Test]
        public void dictionaries_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithDictionaries ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithDictionaries>>();
            var oD = f.Create( o =>
            {
                o.DicOfDic.Add( 1, new Dictionary<object, object?> { { 1, 2L }, { "Hello", "World!" }, { "Goodbye", null } } );
                o.DicOfDic.Add( 2, new Dictionary<object, object?> { { 3, 4 }, { "Hello2", "World2!" } } );
                o.CovariantDicImpl.Add( 42, 3712 );
                o.CovariantDicImpl.Add( 3712, 42 );
                o.CovariantDicNullableImpl.Add( 1, true );
                o.CovariantDicNullableImpl.Add( 2, null );
                o.CovariantDicNullableImpl.Add( 3, false );
            } );
            CheckToString( oD );

            var oD2 = JsonTestHelper.Roundtrip( directory, oD );
            CheckToString( oD2 );

            static void CheckToString( IWithDictionaries o )
            {
                o.ToString().Should().Be( @"
                {
                    ""DicOfDic"":
                        [
                            [[""int"",1],
                              [""M(object,object)"",
                                [
                                  [[""int"",1],[""long"",""2""]],
                                  [[""string"",""Hello""],[""string"",""World!""]],
                                  [[""string"",""Goodbye""],null]
                                ]
                              ]
                            ],
                            [[""int"",2],
                              [""M(object,object)"",
                                [
                                  [[""int"",3],[""int"",4]],
                                  [[""string"",""Hello2""],[""string"",""World2!""]]
                                ]
                              ]
                            ]
                        ],
                    ""CovariantDicImpl"":
                        [
                            [42,3712],
                            [3712,42]
                        ],
                    ""CovariantDicNullableImpl"":
                        [
                            [1,true],
                            [2,null],
                            [3,false]
                        ]
                }"
                .Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
            }
        }

        public interface IWithDynamicObject : IPoco
        {
            IDictionary<string, int> OfInt { get; }
        }

        [Test]
        public void dictionaries_with_string_keys_can_be_objects_or_use_arrays()
        {
            var c = TestHelper.CreateStObjCollector( typeof( CommonPocoJsonSupport ), typeof( IWithDynamicObject ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var o = directory.Create<IWithDynamicObject>( o =>
            {
                o.OfInt.Add( "One", 1 );
                o.OfInt.Add( "Two", 2 );
                o.OfInt.Add( "Three", 3 );
            } );

            // A Dictionary<string,T> is expressed as an object by default.
            o.ToString().Should().Be( @"{""OfInt"":{ ""One"": 1, ""Two"": 2, ""Three"": 3 }}".Replace( " ", "" ) );

            var f = s.GetRequiredService<IPocoFactory<IWithDynamicObject>>();
            var oBack = f.ReadJson( @"{""OfInt"":{ ""One"": 1, ""Two"": 2, ""Three"": 3 }}" );
            Debug.Assert( oBack != null );
            oBack.OfInt["Three"].Should().Be( 3 );
            oBack.Should().BeEquivalentTo( o );

            var oBackA = f.ReadJson( @"{""OfInt"":[[""One"",1],[""Two"",2],[""Three"",3]]}" );
            Debug.Assert( oBackA != null );
            oBackA.Should().BeEquivalentTo( oBack );
        }

    }
}
