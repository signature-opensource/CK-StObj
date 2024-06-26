using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Poco.Exc.Json.Tests.BasicTypeTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.Poco.Exc.Json.Tests
{

    [TestFixture]
    public partial class CollectionTests
    {
        [ExternalName( "IWithArray" )]
        [RegisterPocoType( typeof( string ) )]
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithArray ) );
            using var auto = configuration.Run().CreateAutomaticServices();
            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithArray>>();
            var o1 = f.Create( o => o.Result = "This-is-o1!" );
            var o2 = f.Create( o =>
            {
                o.ArrayOfInt = new int[] { 1, 2, 3, 4, 5 };
                o.ArrayOfObject = new object[] { 1, "Hello" };
                o.ArrayOfArrayOfLong = new long[][] { new long[] { 1, 2 }, new long[] { 3, 4, 5 } };
                o.ArrayOfMe = new[] { o1 };
                o.Result = new object[] { o.ArrayOfInt, o.ArrayOfObject, o.ArrayOfArrayOfLong };
            } );
            o2.ToString().Should().Be( """
                {
                    "ArrayOfInt": [1,2,3,4,5],
                    "ArrayOfObject":
                        [
                            ["int",1],
                            ["string","Hello"]
                        ],
                    "ArrayOfArrayOfLong":
                        [
                            ["1","2"],
                            ["3","4","5"]
                        ],
                    "ArrayOfMe":
                        [
                            {
                                "ArrayOfInt": [],
                                "ArrayOfObject": [],
                                "ArrayOfArrayOfLong": [],
                                "ArrayOfMe": null,
                                "Result": ["string","This-is-o1!"]
                            }
                        ],
                    "Result": ["A(object?)",
                                    [
                                        ["A(int)",[1,2,3,4,5]],
                                        ["A(object?)",
                                            [
                                                ["int",1],
                                                ["string","Hello"]
                                            ]
                                        ],
                                        ["A(A(long)?)",
                                            [
                                                ["1","2"],
                                                ["3","4","5"]
                                            ]
                                        ]
                                    ]
                                ]
                }
                """
                .Replace( " ", "" ).ReplaceLineEndings( "" ) );
        }

        [RegisterPocoType( typeof( List<int> ) )]
        [RegisterPocoType( typeof( List<object> ) )]
        [RegisterPocoType( typeof(object[]) ) ]
        public interface IWithLists : IPoco
        {
            IList<object> ListOfList { get; }

            IList<NormalizedCultureInfo> ListOfNC { get; }

            IList<ExtendedCultureInfo> ListOfEC { get; }

            IList<int> CovariantListImpl { get; }

            IList<int?> CovariantListNullableImpl { get; }

            object? Result { get; set; }
        }

        [Test]
        public void lists_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithLists ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithLists>>();
            var oL = f.Create( o =>
            {
                o.ListOfList.Add( new List<int> { 1, 2 } );
                o.ListOfList.Add( new List<int> { 3, 4, 5 } );
                o.ListOfNC.AddRangeArray( NormalizedCultureInfo.Invariant, NormalizedCultureInfo.CodeDefault );
                o.ListOfEC.AddRangeArray( NormalizedCultureInfo.CodeDefault, ExtendedCultureInfo.EnsureExtendedCultureInfo( "es,fr,de" ) );
                o.CovariantListImpl.Add( 42 );
                o.CovariantListImpl.Add( 3712 );
                o.CovariantListNullableImpl.Add( null );
                o.CovariantListNullableImpl.Add( 0 );
                o.CovariantListNullableImpl.Add( null );
                o.Result = new object[] { o.ListOfNC, o.ListOfEC, o.ListOfList, o.CovariantListImpl, o.CovariantListNullableImpl };
            } );
            CheckToString( oL );

            var oL2 = JsonTestHelper.Roundtrip( directory, oL );
            CheckToString( oL2 );

            static void CheckToString( IWithLists oD )
            {
                oD.ToString().Should().Be( """
                    {
                        "ListOfList": [["L(int)",[1,2]],["L(int)",[3,4,5]]],
                        "ListOfNC": ["","en"],
                        "ListOfEC": [["NormalizedCultureInfo","en"],["ExtendedCultureInfo","es,fr,de"]],
                        "CovariantListImpl": [42,3712],
                        "CovariantListNullableImpl": [null,0,null],
                        "Result": ["A(object?)",
                                        [
                                            ["L(NormalizedCultureInfo?)", ["","en"]],
                                            ["L(ExtendedCultureInfo?)", [["NormalizedCultureInfo","en"],["ExtendedCultureInfo","es,fr,de"]]],
                                            ["L(object?)",[["L(int)", [1,2]],["L(int)",[3,4,5]]]],
                                            ["L(int)", [42,3712]],
                                            ["L(int?)", [null,0,null]]
                                        ]
                                    ]
                    }
                    """.Replace( " ", "" ).ReplaceLineEndings( "" ) );
            }
        }

        [CKTypeDefiner]
        public interface ISomeAbstract : IPoco
        {
            string Name { get; set; }
        }

        [ExternalName("Concrete")]
        public interface IConcrete : ISomeAbstract
        {
        }

        public interface ISecondary : IConcrete
        {
        }

        [RegisterPocoType( typeof( object[] ) )]
        public interface IWithSets : IPoco
        {
            ISet<NormalizedCultureInfo> SetOfNC { get; }

            ISet<ExtendedCultureInfo> SetOfEC { get; }

            ISet<int> CovariantSetImpl { get; }

            ISet<int?> CovariantSetNullableImpl { get; }

            object? Result { get; set; }
        }

        [Test]
        public void sets_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithSets ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var oS = directory.Create<IWithSets>( o =>
            {
                o.SetOfNC.AddRangeArray( NormalizedCultureInfo.Invariant, NormalizedCultureInfo.CodeDefault );
                o.SetOfEC.AddRangeArray( NormalizedCultureInfo.CodeDefault, ExtendedCultureInfo.EnsureExtendedCultureInfo( "es, de, fr" ) );
                o.CovariantSetImpl.Add( 42 );
                o.CovariantSetImpl.Add( 3712 );
                o.CovariantSetNullableImpl.Add( null );
                o.CovariantSetNullableImpl.Add( 0 );
                o.CovariantSetNullableImpl.Add( 1 );
                o.Result = new object[] { o.SetOfNC, o.SetOfEC, o.CovariantSetImpl, o.CovariantSetNullableImpl };
            } );

            CheckToString( oS );

            var oS2 = JsonTestHelper.Roundtrip( directory, oS );
            CheckToString( oS2 );

            static void CheckToString( IWithSets o )
            {
                o.ToString().Should().Be( @"
                {
                    ""SetOfNC"":["""",""en""],
                    ""SetOfEC"":[[""NormalizedCultureInfo"",""en""],[""ExtendedCultureInfo"",""es,de,fr""]],
                    ""CovariantSetImpl"":[42,3712],
                    ""CovariantSetNullableImpl"":[null,0,1],
                    ""Result"": [""A(object?)"",
                                    [
                                        [""S(NormalizedCultureInfo?)"",["""",""en""]],
                                        [""S(ExtendedCultureInfo?)"",[[""NormalizedCultureInfo"",""en""],[""ExtendedCultureInfo"",""es,de,fr""]]],
                                        [""S(int)"",[42,3712]],
                                        [""S(int?)"",[null,0,1]]
                                    ]
                                ]
                }"
                .Replace( " ", "" ).Replace( "\r", "" ).Replace( "\n", "" ) );
            }
        }

        [RegisterPocoType( typeof( long ) )]
        [RegisterPocoType( typeof( Dictionary<string, object> ) )]
        [RegisterPocoType( typeof( Dictionary<int,object> ) )]
        [RegisterPocoType( typeof( object[] ) )]
        public interface IWithDictionaries : IPoco
        {
            IDictionary<string, object> DicOfDic { get; }

            IDictionary<int, int> CovariantDicImpl { get; }

            IDictionary<int, bool?> CovariantDicNullableImpl { get; }

            object? Result { get; set; }
        }

        [Test]
        public void dictionaries_serialization()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithDictionaries ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithDictionaries>>();
            var oD = f.Create( o =>
            {
                o.DicOfDic.Add( "One", new Dictionary<string, object?> { { "Hi...", 42L }, { "Hello", "World!" }, { "Goodbye", null } } );
                o.DicOfDic.Add( "Two", new Dictionary<int, object?> { { 1, -1L }, { 2, "World2!" }, { 42, null } } );
                o.CovariantDicImpl.Add( 42, 3712 );
                o.CovariantDicImpl.Add( 3712, 42 );
                o.CovariantDicNullableImpl.Add( 1, true );
                o.CovariantDicNullableImpl.Add( 2, null );
                o.CovariantDicNullableImpl.Add( 3, false );
                o.Result = new object[] { o.DicOfDic, o.CovariantDicImpl, o.CovariantDicNullableImpl };
            } );
            CheckToString( oD );

            var oD2 = JsonTestHelper.Roundtrip( directory, oD );
            CheckToString( oD2 );

            static void CheckToString( IWithDictionaries o )
            {
                o.ToString().Should().Be( @"
                {
                    ""DicOfDic"":
                        {
                            ""One"": [""O(object?)"",
                                         {
                                           ""Hi..."": [""long"",""42""],
                                           ""Hello"": [""string"",""World!""],
                                           ""Goodbye"": null
                                         }
                                     ],
                            ""Two"": [""M(int,object?)"",
                                         [
                                           [1,[""long"",""-1""]],
                                           [2,[""string"",""World2!""]],
                                           [42,null]
                                         ]
                                     ]
                        },
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
                        ],
                    ""Result"": [""A(object?)"",
                                    [
                                        [""O(object?)"",
                                            {
                                                ""One"": [""O(object?)"",
                                                             {
                                                               ""Hi..."": [""long"",""42""],
                                                               ""Hello"": [""string"",""World!""],
                                                               ""Goodbye"": null
                                                             }
                                                         ],
                                                ""Two"": [""M(int,object?)"",
                                                             [
                                                               [1,[""long"",""-1""]],
                                                               [2,[""string"",""World2!""]],
                                                               [42,null]
                                                             ]
                                                         ]
                                            }
                                        ],
                                        [""M(int,int)"",
                                            [
                                                [42,3712],
                                                [3712,42]
                                            ]
                                        ],
                                        [""M(int,bool?)"",
                                            [
                                                [1,true],
                                                [2,null],
                                                [3,false]
                                            ]
                                        ]
                                    ]
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithDynamicObject ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var o = directory.Create<IWithDynamicObject>( o =>
            {
                o.OfInt.Add( "One", 1 );
                o.OfInt.Add( "Two", 2 );
                o.OfInt.Add( "Three", 3 );
            } );

            // A Dictionary<string,T> is expressed as an object by default.
            o.ToString().Should().Be( @"{""OfInt"":{ ""One"": 1, ""Two"": 2, ""Three"": 3 }}".Replace( " ", "" ) );

            var f = auto.Services.GetRequiredService<IPocoFactory<IWithDynamicObject>>();
            var oBack = f.ReadJson( @"{""OfInt"":{ ""One"": 1, ""Two"": 2, ""Three"": 3 }}" );
            Debug.Assert( oBack != null );
            oBack.OfInt["Three"].Should().Be( 3 );
            oBack.Should().BeEquivalentTo( o );

            var oBackA = f.ReadJson( @"{""OfInt"":[[""One"",1],[""Two"",2],[""Three"",3]]}" );
            Debug.Assert( oBackA != null );
            oBackA.Should().BeEquivalentTo( oBack );
        }

        public record struct Rec( object Obj );

        [RegisterPocoType( typeof( Rec ) )]
        public interface IAllCollectionOfObjects : IPoco
        {
            IList<object> List { get; }

            IDictionary<int, object> Dictionary { get; }
        }

        [NotExchangeable]
        public interface IThing : IPoco
        {
            string Name { get; set; }
            int Power { get; set; }
        }

        [Test]
        public void PocoTypeSet_filtering_can_lead_to_invalid_Poco()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IAllCollectionOfObjects ), typeof( IThing ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var oneThing = directory.Create<IThing>( c => { c.Name = "Here"; c.Power = 42; } );
            var o = directory.Create<IAllCollectionOfObjects>( o =>
            {
                o.List.Add( "One" );
                o.List.Add( 1 );
                o.List.Add( oneThing );
                o.List.Add( new Rec( oneThing ) );
                o.List.Add( new Rec( "a-string" ) );

                o.Dictionary.Add( 1, 1 );
                o.Dictionary.Add( 2, "One" );
                o.Dictionary.Add( 3, directory.Create<IThing>( c => c.Name = "World!" ) );
                o.Dictionary.Add( 4, new Rec( Obj: oneThing ) );
                o.Dictionary.Add( 5, new Rec( Obj: 3712 ) );
            } );

            var allW = new PocoJsonExportOptions() { TypeFilterName = "AllSerializable" };
            var allR = new PocoJsonImportOptions() { TypeFilterName = "AllSerializable" };
            {
                string? sText = null;
                var o2 = JsonTestHelper.Roundtrip( directory, o, allW, allR, text => sText = text );
                sText.Should().Be( """
                ["CK.Poco.Exc.Json.Tests.CollectionTests.IAllCollectionOfObjects",
                {
                    "list":
                        [
                            ["string","One"],
                            ["int",1],
                            ["CK.Poco.Exc.Json.Tests.CollectionTests.IThing",{"name":"Here","power":42}],
                            ["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{"obj":["CK.Poco.Exc.Json.Tests.CollectionTests.IThing",{"name":"Here","power":42}]}],
                            ["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{"obj":["string","a-string"]}]
                        ],
                    "dictionary":
                        [
                            [1,["int",1]],
                            [2,["string","One"]],
                            [3,["CK.Poco.Exc.Json.Tests.CollectionTests.IThing",{"name":"World!","power":0}]],
                            [4,["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{"obj":["CK.Poco.Exc.Json.Tests.CollectionTests.IThing",{"name":"Here","power":42}]}]],
                            [5,["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{"obj":["int",3712]}]]
                        ]
                }]
                """.Replace( " ", "" ).ReplaceLineEndings( "" ) );
                o2.List.Should().HaveCount( 5 );
                o2.Dictionary.Should().HaveCount( 5 );
            }

            var excW = new PocoJsonExportOptions() { TypeFilterName = "AllExchangeable" };
            var filtered = o.ToString( excW, withType: true );
            filtered.Should().Be( """
                ["CK.Poco.Exc.Json.Tests.CollectionTests.IAllCollectionOfObjects",
                {
                    "list":
                        [
                            ["string","One"],
                            ["int",1],
                            ["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{}],
                            ["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{"obj":["string","a-string"]}]
                        ],
                    "dictionary":
                        [
                            [1,["int",1]],
                            [2,["string","One"]],
                            [4,["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{}]],
                            [5,["CK.Poco.Exc.Json.Tests.CollectionTests.Rec",{"obj":["int",3712]}]]
                        ]
                }]
                """.Replace( " ", "" ).ReplaceLineEndings( "" ) );
            // The filtered output can be read back but it is invalid:
            // - The two Rec that have a IThing as their `Obj` will have no `Obj` reference.
            // - The Rec.Obj is a not null `object`.
            // => The result is invalid.
            //
            // One may handle this case: it is possible to predict that filtering out a value of a non nullable field
            // will lead to an invalid object. But this can be done only for basic validation rules, there is no way
            // for ad-hoc CheckValid to be handled.
            // 
            // Filtering Poco type cannot offer any guaranty in terms of data validity. Here, writing back the result
            // throws and this is "normal".
            //
            var excR = new PocoJsonImportOptions() { TypeFilterName = "AllExchangeable" };
            var o3 = directory.ReadJson( filtered, excR ) as IAllCollectionOfObjects;
            Throw.DebugAssert( o3 != null );
            var invalid1 = (Rec)o3.List[2];
            invalid1.Obj.Should().BeNull();
            var invalid2 = (Rec)o3.Dictionary[4];
            invalid2.Obj.Should().BeNull();
            FluentActions.Invoking( () => o3.ToString( excW ) ).Should().Throw<Exception>();
        }

        [RegisterPocoType( typeof( object[] ) )]
        public interface IWithListsA : IPoco
        {
            IList<ISomeAbstract> ListOfAbstract { get; }

            IList<IConcrete> ListOfConcrete { get; }

            IList<ISecondary> ListOfSecondary { get; }

            IList<ISomeAbstract?> ListOfNullableAbstract { get; }

            IList<IConcrete?> ListOfNullableConcrete { get; }

            IList<ISecondary?> ListOfNullableSecondary { get; }

            object? Result { get; set; }
        }

        [Test]
        public void lists_serialization_with_abstracts()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithListsA ), typeof( ISecondary ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var p1 = directory.Create<IConcrete>( o => o.Name = "c" );
            var p2 = directory.Create<ISecondary>( o => o.Name = "s" );
            var oL = directory.Create<IWithListsA>( oL =>
            {
                oL.ListOfAbstract.AddRangeArray( p1, p2 );
                oL.ListOfConcrete.AddRangeArray( p1, p2 );
                oL.ListOfSecondary.Add( p2 );
                oL.ListOfNullableAbstract.Add( null );
                oL.ListOfNullableConcrete.Add( null );
                oL.ListOfNullableSecondary.Add( null );
                oL.Result = new object[] { oL.ListOfAbstract, oL.ListOfConcrete, oL.ListOfSecondary, oL.ListOfNullableAbstract, oL.ListOfNullableConcrete, oL.ListOfNullableSecondary };
            } );
            CheckToString( oL );

            var oL2 = JsonTestHelper.Roundtrip( directory, oL );
            CheckToString( oL2 );

            static void CheckToString( IWithListsA oL )
            {
                oL.ToString().Should().Be( """
                {
                    "ListOfAbstract":
                        [
                            ["Concrete",{"Name":"c"}],
                            ["Concrete",{"Name":"s"}]
                        ],
                    "ListOfConcrete": [{"Name":"c"},{"Name":"s"}],
                    "ListOfSecondary":[{"Name":"s"}],
                    "ListOfNullableAbstract":[null],
                    "ListOfNullableConcrete":[null],
                    "ListOfNullableSecondary":[null],
                    "Result":
                        ["A(object?)",
                            [
                                ["L(CK.Poco.Exc.Json.Tests.CollectionTests.ISomeAbstract?)",
                                    [
                                        ["Concrete",{"Name":"c"}],
                                        ["Concrete",{"Name":"s"}]
                                    ]
                                ],
                                ["L(Concrete?)",[{"Name":"c"},{"Name":"s"}]],
                                ["L(Concrete?)",[{"Name":"s"}]],
                                ["L(CK.Poco.Exc.Json.Tests.CollectionTests.ISomeAbstract?)",[null]],
                                ["L(Concrete?)",[null]],
                                ["L(Concrete?)",[null]]
                            ]
                        ]
                }
                """.Replace( " ", "" ).ReplaceLineEndings( "" ) );
            }
        }

        [RegisterPocoType( typeof( object[] ) )]
        public interface IWithDicsA : IPoco
        {
            IDictionary<NormalizedCultureInfo,ISomeAbstract> DicOfAbstract { get; }

            IDictionary<NormalizedCultureInfo, IConcrete> DicOfConcrete { get; }

            IDictionary<NormalizedCultureInfo,ISecondary> DicOfSecondary { get; }

            IDictionary<int,ISomeAbstract?> DicOfNullableAbstract { get; }

            IDictionary<int,IConcrete?> DicOfNullableConcrete { get; }

            IDictionary<int,ISecondary?> DicOfNullableSecondary { get; }

            object? Result { get; set; }
        }

        [Test]
        public void dictionaries_serialization_with_abstracts()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( CommonPocoJsonSupport ), typeof( IWithDicsA ), typeof( ISecondary ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var directory = auto.Services.GetRequiredService<PocoDirectory>();

            var c1 = NormalizedCultureInfo.EnsureNormalizedCultureInfo( "es" );
            var c2 = NormalizedCultureInfo.EnsureNormalizedCultureInfo( "de" );
            var p1 = directory.Create<IConcrete>( o => o.Name = "c" );
            var p2 = directory.Create<ISecondary>( o => o.Name = "s" );
            var oD = directory.Create<IWithDicsA>( oD =>
            {
                oD.DicOfAbstract.Add( c1, p1 );
                oD.DicOfAbstract.Add( c2, p2 );
                oD.DicOfConcrete.Add( c1, p1 );
                oD.DicOfConcrete.Add( c2, p2 );
                oD.DicOfSecondary.Add( NormalizedCultureInfo.CodeDefault, p2 );
                oD.DicOfNullableAbstract.Add( 0, null );
                oD.DicOfNullableConcrete.Add( 0, null );
                oD.DicOfNullableSecondary.Add( 0, null );
                oD.Result = new object[] { oD.DicOfAbstract, oD.DicOfConcrete, oD.DicOfSecondary, oD.DicOfNullableAbstract, oD.DicOfNullableConcrete, oD.DicOfNullableSecondary };
            } );
            CheckToString( oD );

            var oL2 = JsonTestHelper.Roundtrip( directory, oD );
            CheckToString( oL2 );

            static void CheckToString( IWithDicsA oD )
            {
                oD.ToString().Should().Be( """
                    {
                        "DicOfAbstract":
                            [
                                ["es",["Concrete",{"Name":"c"}]],
                                ["de",["Concrete",{"Name":"s"}]]
                            ],
                        "DicOfConcrete":
                            [
                                ["es",{"Name":"c"}],
                                ["de",{"Name":"s"}]
                            ],
                        "DicOfSecondary":
                            [
                                ["en",{"Name":"s"}]
                            ],
                        "DicOfNullableAbstract":
                            [
                                [0,null]
                            ],
                        "DicOfNullableConcrete":
                            [
                                [0,null]
                            ],
                        "DicOfNullableSecondary":
                            [
                                [0,null]
                            ],
                        "Result":
                            ["A(object?)",
                                [
                                    ["M(NormalizedCultureInfo,CK.Poco.Exc.Json.Tests.CollectionTests.ISomeAbstract?)",
                                        [
                                            ["es",["Concrete",{"Name":"c"}]],
                                            ["de",["Concrete",{"Name":"s"}]]
                                        ]
                                    ],
                                    ["M(NormalizedCultureInfo,Concrete?)",[["es",{"Name":"c"}],["de",{"Name":"s"}]]],
                                    ["M(NormalizedCultureInfo,Concrete?)",[["en",{"Name":"s"}]]],
                                    ["M(int,CK.Poco.Exc.Json.Tests.CollectionTests.ISomeAbstract?)",[[0,null]]],
                                    ["M(int,Concrete?)",[[0,null]]],["M(int,Concrete?)",[[0,null]]]
                                ]
                        ]
                    }
                    """.Replace( " ", "" ).ReplaceLineEndings( "" ) );
            }
        }

    }
}
