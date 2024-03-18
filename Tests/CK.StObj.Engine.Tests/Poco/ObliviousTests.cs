using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class ObliviousTests
    { 
        [ExternalName( "ExternalNameForVerySimplePoco" )]
        public interface IVerySimplePoco : IPoco
        {
            int Value { get; set; }
        }

        // We need (at least) a secondary IPoco to test the CK.GRSupport.PocoList_*_CK adapter.
        // For list, when there's only the primary, we use the List<primary> that is enough because
        // IReadOnlyList is covariant and there's no list implementation to adapt.
        public interface ISecondaryVerySimplePoco : IVerySimplePoco
        {
        }

        public List<int> ListV = null!; // Oblivious
        public List<int?> ListNV = null!; // Oblivious

        public List<object> ListR = null!; // Oblivious
        public List<object?> ListNR = null!;

        public List<IVerySimplePoco> ListPR = null!; // Oblivious
        public List<IVerySimplePoco?> ListPNR = null!;

        public interface IProperListDefinition : IPoco
        {
            IList<int?> IListNV { get; }
            IList<int> IListV { get; }
            IList<object?> IListNR { get; }
            IList<object> IListR { get; }
            IList<IVerySimplePoco?> IListPNR { get; }
            IList<IVerySimplePoco> IListPR { get; }
        }

        [TestCase( false )]
        [TestCase( true )]
        public void oblivious_List( bool revert )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ),
                                                     typeof( ISecondaryVerySimplePoco ),
                                                     typeof( IProperListDefinition ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.PocoTypeSystemBuilder;

            if( !revert )
            {
                // List of Value type
                // List<int> (oblivious, final)
                ICollectionPocoType? tRV = List_Int( ts );
                // List<int?> (oblivious, final)
                ICollectionPocoType? tRNV = List_IntN( ts );
                // IList<int> (oblivious, final)
                ICollectionPocoType? tIV = IList_Int( ts );
                // IList<int?> (oblivious, final)
                ICollectionPocoType? tINV = IList_IntN( ts );

                // List of Reference type (object)
                // List<object> (oblivious, final)
                ICollectionPocoType? tRR = List_Object( ts );
                // List<object?> (non oblivious, non final)
                ICollectionPocoType? tRNR = List_ObjectN( revert, ts, tRR );
                // IList<object> (oblivious, non final)
                ICollectionPocoType? tIR = IList_Object( revert, ts, tRR );
                // IList<object?> (non oblivious, non final)
                ICollectionPocoType? tINR = IList_ObjectN( revert, ts, tIR, tRR );

                // List of Reference type but IVerySimplePoco.
                var n = typeof( IVerySimplePoco ).ToCSharpName();
                // List<IVerySimplePoco> (oblivious, final)
                ICollectionPocoType? tPRR = List_Poco( ts, n );
                // List<IVerySimplePoco?> (non oblivious, non final)
                ICollectionPocoType? tPRNR = List_PocoN( revert, ts, n, tPRR );
                // IList<IVerySimplePoco> (oblivious, final)
                ICollectionPocoType? tPIR = IList_Poco( revert, ts, n );
                // IList<IVerySimplePoco?> (non oblivious, non final)
                ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n, tPIR );
            }
            else
            {
                // List of Reference type but IVerySimplePoco.
                var n = typeof( IVerySimplePoco ).ToCSharpName();
                // IList<IVerySimplePoco?> (non oblivious, non final)
                ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n, null! );
                // IList<IVerySimplePoco> (oblivious, final)
                ICollectionPocoType? tPIR = IList_Poco( revert, ts, n );
                // List<IVerySimplePoco?> (non oblivious, non final)
                ICollectionPocoType? tPRNR = List_PocoN( revert, ts, n, null! );
                // List<IVerySimplePoco (oblivious, final)
                ICollectionPocoType? tPRR = List_Poco( ts, n );

                // List of Reference type (object)
                // IList<object?> (non oblivious, non final)
                ICollectionPocoType? tINR = IList_ObjectN( revert, ts, null!, null! );
                // IList<object> (oblivious, non final)
                ICollectionPocoType? tIR = IList_Object( revert, ts, null! );
                // List<object?> (oblivious, final)
                ICollectionPocoType? tRNR = List_ObjectN( revert, ts, null! );
                // List<object> (oblivious, final)
                ICollectionPocoType? tRR = List_Object( ts );

                // List of Value type
                // IList<int?> (oblivious, final)
                ICollectionPocoType? tINV = IList_IntN( ts );
                // IList<int> (oblivious, final)
                ICollectionPocoType? tIV = IList_Int( ts );
                // List<int?> (oblivious, final)
                ICollectionPocoType? tRNV = List_IntN( ts );
                // List<int> (oblivious, final)
                ICollectionPocoType? tRV = List_Int( ts );

                tINR.ObliviousType.Should().BeSameAs( tIR );
                tINR.StructuralFinalType.Should().BeSameAs( tRR );

                tIR.StructuralFinalType.Should().BeSameAs( tRR );

                tPRNR.ObliviousType.Should().BeSameAs( tPRR );

                tPINR.ObliviousType.Should().BeSameAs( tPIR );
            }

            ICollectionPocoType List_Int( IPocoTypeSystemBuilder ts )
            {
                var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListV ) )! );
                Debug.Assert( tRV != null );
                tRV.IsOblivious.Should().BeTrue();
                tRV.Nullable.IsOblivious.Should().BeFalse();
                tRV.CSharpName.Should().Be( "List<int>" );
                tRV.ImplTypeName.Should().Be( "List<int>" );
                tRV.IsStructuralFinalType.Should().BeTrue();
                return tRV;
            }

            ICollectionPocoType List_IntN( IPocoTypeSystemBuilder ts )
            {
                var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNV ) )! );
                Debug.Assert( tRNV != null );
                tRNV.IsOblivious.Should().BeTrue();
                tRNV.Nullable.IsOblivious.Should().BeFalse();
                tRNV.CSharpName.Should().Be( "List<int?>" );
                tRNV.ImplTypeName.Should().Be( "List<int?>" );
                tRNV.IsStructuralFinalType.Should().BeTrue();
                return tRNV;
            }

            ICollectionPocoType IList_Int( IPocoTypeSystemBuilder ts )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tIV = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListV ) ).Type;
                Debug.Assert( tIV != null );
                tIV.IsOblivious.Should().BeTrue();
                tIV.CSharpName.Should().Be( "IList<int>" );
                tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueList<int>" );
                tIV.IsStructuralFinalType.Should().BeTrue();
                return tIV;
            }

            ICollectionPocoType IList_IntN( IPocoTypeSystemBuilder ts )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tINV = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListNV ) ).Type;
                Debug.Assert( tINV != null );
                tINV.IsOblivious.Should().BeTrue();
                tINV.CSharpName.Should().Be( "IList<int?>" );
                tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueList<int>" );
                tINV.IsStructuralFinalType.Should().BeTrue();
                return tINV;
            }

            ICollectionPocoType List_Object( IPocoTypeSystemBuilder ts )
            {
                var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListR ) )! );
                Debug.Assert( tRR != null );
                tRR.IsOblivious.Should().BeTrue();
                tRR.Nullable.IsOblivious.Should().BeFalse();
                tRR.CSharpName.Should().Be( "List<object>" );
                tRR.ImplTypeName.Should().Be( "List<object>" );
                tRR.IsStructuralFinalType.Should().BeTrue();
                return tRR;
            }

            ICollectionPocoType List_ObjectN( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRR )
            {
                var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNR ) )! );
                Debug.Assert( tRNR != null );
                tRNR.IsOblivious.Should().BeFalse();
                tRNR.CSharpName.Should().Be( "List<object?>" );
                tRNR.ImplTypeName.Should().Be( "List<object>" );
                tRNR.IsStructuralFinalType.Should().BeFalse();
                if( !revert ) tRNR.ObliviousType.Should().BeSameAs( tRR );
                return tRNR;
            }

            ICollectionPocoType IList_Object( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tIR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListR ) ).Type;
                Debug.Assert( tIR != null );
                tIR.IsOblivious.Should().BeTrue();
                tIR.CSharpName.Should().Be( "IList<object>" );
                tIR.ImplTypeName.Should().Be( "List<object>" );
                tIR.IsStructuralFinalType.Should().BeFalse();
                if( !revert ) tIR.StructuralFinalType.Should().BeSameAs( tRR );
                return tIR;
            }

            ICollectionPocoType IList_ObjectN( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tIR, ICollectionPocoType tRR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tINR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListNR ) ).Type;
                Debug.Assert( tINR != null );
                tINR.IsOblivious.Should().BeFalse();
                tINR.CSharpName.Should().Be( "IList<object?>" );
                tINR.ImplTypeName.Should().Be( "List<object>" );
                tINR.IsStructuralFinalType.Should().BeFalse();
                if( !revert )
                {
                    tINR.ObliviousType.Should().BeSameAs( tIR );
                    tINR.StructuralFinalType.Should().BeSameAs( tRR );
                }
                return tINR;
            }

            ICollectionPocoType List_Poco( IPocoTypeSystemBuilder ts, string n )
            {
                var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPR ) )! );
                Debug.Assert( tPRR != null );
                tPRR.IsOblivious.Should().BeTrue();
                tPRR.Nullable.IsOblivious.Should().BeFalse();
                tPRR.CSharpName.Should().Be( $"List<{n}>" );
                tPRR.ImplTypeName.Should().Be( $"List<{n}>" );
                return tPRR;
            }

            ICollectionPocoType List_PocoN( bool revert, IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tPRR )
            {
                var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPNR ) )! );
                Debug.Assert( tPRNR != null );
                tPRNR.IsOblivious.Should().BeFalse();
                tPRNR.CSharpName.Should().Be( $"List<{n}?>" );
                tPRNR.ImplTypeName.Should().Be( $"List<{n}>" );
                if( !revert ) tPRNR.ObliviousType.Should().BeSameAs( tPRR );
                return tPRNR;
            }

            ICollectionPocoType IList_Poco( bool revert, IPocoTypeSystemBuilder ts, string n )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tPIR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListPR ) ).Type;
                Debug.Assert( tPIR != null );
                tPIR.IsOblivious.Should().BeTrue();
                tPIR.CSharpName.Should().Be( $"IList<{n}>" );
                tPIR.IsStructuralFinalType.Should().BeTrue();
                tPIR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoList_*_CK" );
                return tPIR;
            }

            ICollectionPocoType IList_PocoN( bool revert, IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tPIR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tPINR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListPNR ) ).Type;
                Debug.Assert( tPINR != null );
                tPINR.IsOblivious.Should().BeFalse();
                tPINR.CSharpName.Should().Be( $"IList<{n}?>" );
                if( !revert )
                {
                    tPINR.ImplTypeName.Should().Be( tPIR.ImplTypeName );
                    tPINR.ObliviousType.Should().BeSameAs( tPIR );
                    tPINR.StructuralFinalType.Should().BeSameAs( tPIR );
                }
                return tPINR;
            }

        }

        public Dictionary<string, int> DicV = null!; // Oblivious
        public Dictionary<string, int?> DicNV = null!; // Oblivious

        public Dictionary<int, object> DicR = null!; // Oblivious
        public Dictionary<int, object?> DicNR = null!;

        public Dictionary<int, IVerySimplePoco> DicPR = null!; // Oblivious
        public Dictionary<int, IVerySimplePoco?> DicPNR = null!;


        public interface IProperDictionaryDefinition : IPoco
        {
            IDictionary<string, int?> IDicNV { get; }
            IDictionary<string, int> IDicV { get; }
            IDictionary<int, object?> IDicNR { get; }
            IDictionary<int, object> IDicR { get; }
            IDictionary<int, IVerySimplePoco?> IDicPNR { get; }
            IDictionary<int, IVerySimplePoco> IDicPR { get; }
        }

        [Test]
        public void Oblivious_and_Final_Dictionary()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ), typeof( IProperDictionaryDefinition) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.PocoTypeSystemBuilder;

            // Dictionary of Value type for the value (int)

            // Dictionary<string,int> (oblivious, final).
            var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicV ) )! );
            Debug.Assert( tRV != null );
            tRV.IsOblivious.Should().BeTrue();
            tRV.CSharpName.Should().Be( "Dictionary<string,int>" );
            tRV.ImplTypeName.Should().Be( "Dictionary<string,int>" );
            tRV.IsFinalType.Should().BeTrue();

            // Dictionary<string,int?> (oblivious, final).
            var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNV ) )! );
            Debug.Assert( tRNV != null );
            tRNV.IsOblivious.Should().BeTrue();
            tRNV.CSharpName.Should().Be( "Dictionary<string,int?>" );
            tRNV.ImplTypeName.Should().Be( "Dictionary<string,int?>" );
            tRNV.IsFinalType.Should().BeTrue();

            var defPoco = ts.FindByType<IPrimaryPocoType>( typeof( IProperDictionaryDefinition ) );
            Throw.DebugAssert( defPoco != null );

            // IDictionary<string,int> (oblivious, final)
            var tIV = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == "IDicV" ).Type;
            Debug.Assert( tIV != null );
            tIV.IsOblivious.Should().BeTrue();
            tIV.CSharpName.Should().Be( "IDictionary<string,int>" );
            tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueDictionary<string,int>" );
            tIV.IsStructuralFinalType.Should().BeTrue();

            // IDictionary<string,int?> (oblivious, final)
            var tINV = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == "IDicNV" ).Type;
            Debug.Assert( tINV != null );
            tINV.IsOblivious.Should().BeTrue();
            tINV.CSharpName.Should().Be( "IDictionary<string,int?>" );
            tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueDictionary<string,int>" );
            tINV.IsStructuralFinalType.Should().BeTrue();

            ////// Dictionary of reference type (object) for the value.

            // Dictionary<int,object> (oblivious, final).
            var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicR ) )! );
            Debug.Assert( tRR != null );
            tRR.IsOblivious.Should().BeTrue();
            tRR.CSharpName.Should().Be( "Dictionary<int,object>" );
            tRR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tRR.IsStructuralFinalType.Should().BeTrue();

            // IDictionary<int,object> (oblivious, non final)
            var tIR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicR ) ).Type;
            Debug.Assert( tIR != null );
            tIR.IsOblivious.Should().BeTrue();
            tIR.CSharpName.Should().Be( "IDictionary<int,object>" );
            tIR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tIR.IsStructuralFinalType.Should().BeFalse();

            // Dictionary<int,object?> (non oblivious, non final)
            var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNR ) )! );
            Debug.Assert( tRNR != null );
            tRNR.IsOblivious.Should().BeFalse();
            tRNR.CSharpName.Should().Be( "Dictionary<int,object?>" );
            tRNR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tRNR.ObliviousType.Should().BeSameAs( tRR );
            tRNR.IsStructuralFinalType.Should().BeFalse();

            // IDictionary<int,object?> (non oblivious, non final)
            var tINR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicNR ) ).Type;
            Debug.Assert( tINR != null );
            tINR.IsOblivious.Should().BeFalse();
            tINR.CSharpName.Should().Be( "IDictionary<int,object?>" );
            tINR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tINR.ObliviousType.Should().BeSameAs( tIR );
            tINR.IsStructuralFinalType.Should().BeFalse();

            // Dictionary of IPoco type (IVerySimplePoco) for the value.
            var n = typeof( IVerySimplePoco ).ToCSharpName();

            // Dictionary<int,IVerySimplePoco> (oblivious, final)
            var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPR ) )! );
            Debug.Assert( tPRR != null );
            tPRR.IsOblivious.Should().BeTrue();
            tPRR.CSharpName.Should().Be( $"Dictionary<int,{n}>" );
            tPRR.ImplTypeName.Should().Be( $"Dictionary<int,{n}>" );
            tPRR.IsStructuralFinalType.Should().BeTrue();

            // Dictionary<int,IVerySimplePoco?> (non oblivious, non final)
            var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPNR ) )! );
            Debug.Assert( tPRNR != null );
            tPRNR.IsOblivious.Should().BeFalse();
            tPRNR.CSharpName.Should().Be( $"Dictionary<int,{n}?>" );
            tPRNR.ImplTypeName.Should().Be( $"Dictionary<int,{n}>" );
            tPRNR.ObliviousType.Should().BeSameAs( tPRR );
            tPRNR.IsStructuralFinalType.Should().BeFalse();

            // IDictionary<int,IVerySimplePoco> (oblivious, final)
            var tPIR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicPR ) ).Type;
            Debug.Assert( tPIR != null );
            tPIR.IsOblivious.Should().BeTrue();
            tPIR.CSharpName.Should().Be( $"IDictionary<int,{n}>" );
            tPIR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoDictionary_*_*_CK" );
            tPIR.IsStructuralFinalType.Should().BeTrue();

            // IDictionary<int,IVerySimplePoco?> (non oblivious, non final)
            var tPINR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicPNR ) ).Type;
            Debug.Assert( tPINR != null );
            tPINR.IsOblivious.Should().BeFalse();
            tPINR.CSharpName.Should().Be( $"IDictionary<int,{n}?>" );
            tPINR.ImplTypeName.Should().Be( tPIR.ImplTypeName, "Same implementation as the Dictionary<int,IVerySimplePoco>." );
            tPINR.ObliviousType.Should().BeSameAs( tPIR );
            tPINR.IsStructuralFinalType.Should().BeFalse();
        }

        // AnonymousSampleO is the common ObliviousType: 
        public (IVerySimplePoco, List<IVerySimplePoco>) AnonymousSampleO = default;
        public (IVerySimplePoco?, List<IVerySimplePoco> X) AnonymousSample1 = default;
        public (IVerySimplePoco A, List<IVerySimplePoco?> B) AnonymousSample2 = default;
        public (IVerySimplePoco A, List<IVerySimplePoco>? B) AnonymousSample3 = default;
        public (IVerySimplePoco? A, List<IVerySimplePoco?>? B) AnonymousSample4 = default;

        [TestCase( "ObliviousFirst" )]
        [TestCase( "ObliviousLast" )]
        public void oblivious_anonymous_record( string mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.PocoTypeSystemBuilder;

            IRecordPocoType? tO = null;
            if( mode == "ObliviousFirst" ) tO = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSampleO ) )! );

            var t1 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample1 ) )! );
            Debug.Assert( t1 != null );
            var t2 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample2 ) )! );
            Debug.Assert( t2 != null );
            var t3 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample3 ) )! );
            Debug.Assert( t3 != null );
            var t4 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample4 ) )! );
            Debug.Assert( t4 != null );

            if( mode == "ObliviousLast" ) tO = (IRecordPocoType?)ts.FindByType( GetType().GetField( nameof( AnonymousSampleO ) )!.FieldType );
            Debug.Assert( tO != null && tO.IsAnonymous && tO.IsOblivious );

            tO.ObliviousType.Should().BeSameAs( tO );
            t1.ObliviousType.Should().BeSameAs( tO );
            t2.ObliviousType.Should().BeSameAs( tO );
            t3.ObliviousType.Should().BeSameAs( tO );
            t4.ObliviousType.Should().BeSameAs( tO );

            new object[] { tO, t1, t2, t3, t4 }.Distinct().Should().HaveCount( 5, "Different PocoTypes." );
        }

        public List<List<Dictionary<int, object>>> ListCollectionO = null!;
        public (
                    List<List<Dictionary<int, object?>>> C1,
                    List<List<Dictionary<int, object?>?>?> C2,
                    List<List<Dictionary<int, object>?>?> C3,
                    List<List<Dictionary<int, object>>>? C4,
                    List<List<Dictionary<int, object>>?> C5,
                    List<List<Dictionary<int, object>?>> C6
               )
               ListCollections = default!;

        [TestCase( "ObliviousFirst" )]
        [TestCase( "ObliviousLast" )]
        public void oblivious_anonymous_record_and_collections( string mode )
        {
            var ts = new PocoTypeSystemBuilder( new ExtMemberInfoFactory() );

            ICollectionPocoType? tO = null;
            if( mode == "ObliviousFirst" ) tO = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollectionO ) )! );

            IRecordPocoType? others = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollections ) )! );
            Throw.DebugAssert( others != null );

            if( mode == "ObliviousLast" ) tO = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollectionO ) )! );

            Throw.DebugAssert( tO != null );

            var culprits = others.Fields.Where( f => f.Type.ObliviousType != tO );
            culprits.Should().BeEmpty( "All these collections have the same oblivious type." );


            var oA = others.ObliviousType;
            oA.IsOblivious.Should().BeTrue();
            oA.Fields.Where( f => !f.IsUnnamed ).Should().BeEmpty( "The oblivious anonymous record has no field name." );
            oA.Fields.Where( f => f.Type != tO ).Should().BeEmpty( "The oblivious anonymous record has oblivious field types." );
        }

    }
}
