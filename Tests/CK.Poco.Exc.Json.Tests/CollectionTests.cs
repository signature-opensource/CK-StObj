using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{
    [TestFixture]
    public class CollectionTests
    {
        [ExternalName( "IWithArray" )]
        public interface IWithArray : IPoco
        {
            int[] ArrayOfInt { get; set; }

            object[] ArrayOfObject { get; set; }

            long[][] ArrayOfArrayOfLong { get; set; }

            IWithArray[]? ArrayOfMe { get; set; }

            object? Result { get; set; }
        }

        [Test]
        public void arrays_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IWithArray ) );
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
                    ""Result"": [""object[]"",
                                    [
                                        [""int[]"",[1,2]],
                                        [""object[]"",
                                            [
                                                [""int"",1],
                                                [""string"",""Hello""]
                                            ]
                                        ],
                                        [""long[][]"",
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
            List<List<int>> ListOfList { get; }

            IList<int> CovariantListImpl { get; }

            IList<int?> CovariantListNullableImpl { get; }

            object? Result { get; set; }
        }

        [Test]
        public void lists_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IWithLists ) );
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
                    ""ListOfList"": [[1,2],[3,4,5]],
                    ""CovariantListImpl"": [42,3712],
                    ""CovariantListNullableImpl"": [null,0,null],
                    ""Result"": [""object[]"",
                                    [
                                        [""L(L(int))"",[[1,2],[3,4,5]]],
                                        [""L(int)"",[42,3712]],
                                        [""L(int?)"",[null,0,null]]
                                    ]
                                ]
                }"
                                .Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
            }
        }

        public interface IWithSets : IPoco
        {
            HashSet<HashSet<int>> SetOfSet { get; }

            ISet<int> CovariantSetImpl { get; }

            ISet<int?> CovariantSetNullableImpl { get; }
        }

        [Test]
        public void sets_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( IWithSets ) );
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
                    ""SetOfSet"":[[1,2],[3,4,5]],
                    ""CovariantSetImpl"":[42,3712],
                    ""CovariantSetNullableImpl"":[null,0,1]
                }"
                .Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
            }
        }

        public interface IWithDictionaries : IPoco
        {
            Dictionary<int, Dictionary<object, object?>> DicOfDic { get; }

            IDictionary<int, int> CovariantDicImpl { get; }

            IDictionary<int, bool?> CovariantDicNullableImpl { get; }
        }

        [Test]
        public void dictionaries_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonExportSupport ), typeof( PocoJsonImportSupport ), typeof( IWithDictionaries ) );
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
                            [1,[
                                [[""int"",1],[""long"",""2""]],
                                [[""string"",""Hello""],[""string"",""World!""]],
                                [[""string"",""Goodbye""],null]
                               ]
                            ],
                            [2,[
                                [[""int"",3],[""int"",4]],
                                [[""string"",""Hello2""],[""string"",""World2!""]]
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



    }
}
