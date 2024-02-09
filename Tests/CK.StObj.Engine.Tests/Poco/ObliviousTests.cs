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
                // List<int>: This is a oblivious type.
                ICollectionPocoType? tRV = List_Int_IsOblivious( ts );
                // List<int?>: : This is a oblivious type.
                ICollectionPocoType? tRNV = List_IntN_IsOblivious( ts );
                // IList<int>
                ICollectionPocoType? tIV = IList_Int( revert, ts, tRV );
                // IList<int?>
                ICollectionPocoType? tINV = IList_IntN( revert, ts, tRNV );

                // List of Reference type (object)
                // List<object>: This is the oblivious type.
                ICollectionPocoType? tRR = List_Object_Is_Oblivious( ts );
                // List<object?>
                ICollectionPocoType? tRNR = List_ObjectN( revert, ts, tRR );
                // IList<object?>
                ICollectionPocoType? tINR = IList_ObjectN( revert, ts, tRR );
                // IList<object>
                ICollectionPocoType? tIR = IList_Object( revert, ts, tRR );

                // List of Reference type but IVerySimplePoco.
                var n = typeof( IVerySimplePoco ).ToCSharpName();
                // List<IVerySimplePoco>: This is the oblivious.
                ICollectionPocoType? tPRR = List_Poco_IsOblivious( ts, n );
                // List<IVerySimplePoco?>
                ICollectionPocoType? tPRNR = List_PocoN( revert, ts, n, tPRR );
                // IList<IVerySimplePoco?>
                ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n, tPRR );
                // IList<IVerySimplePoco>
                ICollectionPocoType? tPIR = IList_Poco( revert, ts, n, tPRR, tPINR );
            }
            else
            {
                // List of Reference type but IVerySimplePoco.
                var n = typeof( IVerySimplePoco ).ToCSharpName();
                // IList<IVerySimplePoco>
                ICollectionPocoType? tPIR = IList_Poco( revert, ts, n, null!, null! );
                // IList<IVerySimplePoco?>
                ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n, null! );
                // List<IVerySimplePoco?>
                ICollectionPocoType? tPRNR = List_PocoN( revert, ts, n, null! );
                // List<IVerySimplePoco>: This is the oblivious.
                ICollectionPocoType? tPRR = List_Poco_IsOblivious( ts, n );

                // List of Reference type (object)
                // IList<object>
                ICollectionPocoType? tIR = IList_Object( revert, ts, null! );
                // IList<object?>
                ICollectionPocoType? tINR = IList_ObjectN( revert, ts, null! );
                // List<object?>
                ICollectionPocoType? tRNR = List_ObjectN( revert, ts, null! );
                // List<object>: This is the oblivious type.
                ICollectionPocoType? tRR = List_Object_Is_Oblivious( ts );

                // List of Value type
                // IList<int?>
                ICollectionPocoType? tINV = IList_IntN( revert, ts, null! );
                // IList<int>
                ICollectionPocoType? tIV = IList_Int( revert, ts, null! );
                // List<int?>: : This is a oblivious type.
                ICollectionPocoType? tRNV = List_IntN_IsOblivious( ts );
                // List<int>: This is a oblivious type.
                ICollectionPocoType? tRV = List_Int_IsOblivious( ts );

                tIV.ObliviousType.Should().BeSameAs( tRV );
                tINV.ObliviousType.Should().BeSameAs( tRNV );
                tRNR.ObliviousType.Should().BeSameAs( tRR );
                tINR.ObliviousType.Should().BeSameAs( tRR );
                tIR.ObliviousType.Should().BeSameAs( tRR );
                tPRNR.ObliviousType.Should().BeSameAs( tPRR );
                tPINR.ObliviousType.Should().BeSameAs( tPRR );
                tPIR.ObliviousType.Should().BeSameAs( tPRR );
            }

            ICollectionPocoType List_Int_IsOblivious( IPocoTypeSystemBuilder ts )
            {
                var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListV ) )! );
                Debug.Assert( tRV != null );
                tRV.IsOblivious.Should().BeTrue();
                tRV.Nullable.IsOblivious.Should().BeFalse();
                tRV.CSharpName.Should().Be( "List<int>" );
                tRV.ImplTypeName.Should().Be( "List<int>" );
                return tRV;
            }

            ICollectionPocoType List_IntN_IsOblivious( IPocoTypeSystemBuilder ts )
            {
                var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNV ) )! );
                Debug.Assert( tRNV != null );
                tRNV.IsOblivious.Should().BeTrue();
                tRNV.Nullable.IsOblivious.Should().BeFalse();
                tRNV.CSharpName.Should().Be( "List<int?>" );
                tRNV.ImplTypeName.Should().Be( "List<int?>" );
                return tRNV;
            }

            ICollectionPocoType IList_Int( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRV )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tIV = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListV ) ).Type;
                Debug.Assert( tIV != null );
                tIV.IsOblivious.Should().BeFalse();
                tIV.CSharpName.Should().Be( "IList<int>" );
                tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueList<int>" );
                if( !revert ) tIV.ObliviousType.Should().BeSameAs( tRV );
                return tIV;
            }

            ICollectionPocoType IList_IntN( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRNV )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tINV = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListNV ) ).Type;
                Debug.Assert( tINV != null );
                tINV.IsOblivious.Should().BeFalse();
                tINV.CSharpName.Should().Be( "IList<int?>" );
                tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueList<int>" );
                if( !revert ) tINV.ObliviousType.Should().BeSameAs( tRNV );
                return tINV;
            }

            ICollectionPocoType List_Object_Is_Oblivious( IPocoTypeSystemBuilder ts )
            {
                var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListR ) )! );
                Debug.Assert( tRR != null );
                tRR.IsOblivious.Should().BeTrue();
                tRR.Nullable.IsOblivious.Should().BeFalse();
                tRR.CSharpName.Should().Be( "List<object>" );
                tRR.ImplTypeName.Should().Be( "List<object>" );
                return tRR;
            }

            ICollectionPocoType List_ObjectN( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRR )
            {
                var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNR ) )! );
                Debug.Assert( tRNR != null );
                tRNR.IsOblivious.Should().BeFalse();
                tRNR.CSharpName.Should().Be( "List<object?>" );
                tRNR.ImplTypeName.Should().Be( "List<object?>" );
                if( !revert ) tRNR.ObliviousType.Should().BeSameAs( tRR );
                return tRNR;
            }

            ICollectionPocoType IList_ObjectN( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tINR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListNR ) ).Type;
                Debug.Assert( tINR != null && !tINR.IsOblivious );
                tINR.CSharpName.Should().Be( "IList<object?>" );
                tINR.ImplTypeName.Should().Be( "List<object?>" );
                if( !revert ) tINR.ObliviousType.Should().BeSameAs( tRR );
                return tINR;
            }

            ICollectionPocoType IList_Object( bool revert, IPocoTypeSystemBuilder ts, ICollectionPocoType tRR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tIR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListR ) ).Type;
                Debug.Assert( tIR != null && !tIR.IsOblivious );
                tIR.CSharpName.Should().Be( "IList<object>" );
                tIR.ImplTypeName.Should().Be( "List<object>" );
                if( !revert ) tIR.ObliviousType.Should().BeSameAs( tRR );
                return tIR;
            }

            ICollectionPocoType List_Poco_IsOblivious( IPocoTypeSystemBuilder ts, string n )
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
                tPRNR.ImplTypeName.Should().Be( $"List<{n}?>" );
                if( !revert ) tPRNR.ObliviousType.Should().BeSameAs( tPRR );
                return tPRNR;
            }

            ICollectionPocoType IList_PocoN( bool revert, IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tPRR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tPINR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListPNR ) ).Type;
                Debug.Assert( tPINR != null && !tPINR.IsOblivious );
                tPINR.CSharpName.Should().Be( $"IList<{n}?>" );
                tPINR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoList_*_CK" );
                if( !revert ) tPINR.ObliviousType.Should().BeSameAs( tPRR );
                return tPINR;
            }

            ICollectionPocoType IList_Poco( bool revert, IPocoTypeSystemBuilder ts, string n, ICollectionPocoType tPRNR, ICollectionPocoType tPRR )
            {
                var tListDef = ts.FindByType<IPrimaryPocoType>( typeof( IProperListDefinition ) )!;
                var tPIR = (ICollectionPocoType)tListDef.Fields.Single( f => f.Name == nameof( IProperListDefinition.IListPR ) ).Type;
                Debug.Assert( tPIR != null && !tPIR.IsOblivious );
                tPIR.CSharpName.Should().Be( $"IList<{n}>" );
                if( !revert )
                {
                    tPIR.ImplTypeName.Should().Be( tPRR.ImplTypeName, "Same implementation as the IList<IVerySimplePoco?>." );
                    tPIR.ObliviousType.Should().BeSameAs( tPRNR );
                }
                return tPIR;
            }
        }

        public Dictionary<object, int> DicV = null!; // Oblivious
        public Dictionary<object, int?> DicNV = null!; // Oblivious

        public Dictionary<int, object> DicR = null!; // Oblivious
        public Dictionary<int, object?> DicNR = null!;

        public Dictionary<int, IVerySimplePoco> DicPR = null!; // Oblivious
        public Dictionary<int, IVerySimplePoco?> DicPNR = null!;


        public interface IProperDictionaryDefinition : IPoco
        {
            IDictionary<object, int?> IDicNV { get; }
            IDictionary<object, int> IDicV { get; }
            IDictionary<int, object?> IDicNR { get; }
            IDictionary<int, object> IDicR { get; }
            IDictionary<int, IVerySimplePoco?> IDicPNR { get; }
            IDictionary<int, IVerySimplePoco> IDicPR { get; }
        }

        [Test]
        public void oblivious_Dictionary()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ), typeof( IProperDictionaryDefinition) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.PocoTypeSystemBuilder;

            // Dictionary of Value type for the value (int)

            // Dictionary<object,int>: This is the oblivious.
            var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicV ) )! );
            Debug.Assert( tRV != null );
            tRV.IsOblivious.Should().BeTrue();
            tRV.CSharpName.Should().Be( "Dictionary<object,int>" );
            tRV.ImplTypeName.Should().Be( "Dictionary<object,int>" );

            // Dictionary<object,int?>: This is the oblivious.
            var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNV ) )! );
            Debug.Assert( tRNV != null );
            tRNV.IsOblivious.Should().BeTrue();
            tRNV.CSharpName.Should().Be( "Dictionary<object,int?>" );
            tRNV.ImplTypeName.Should().Be( "Dictionary<object,int?>" );

            var defPoco = ts.FindByType<IPrimaryPocoType>( typeof( IProperDictionaryDefinition ) );
            Throw.DebugAssert( defPoco != null );

            // IDictionary<object,int>
            var tIV = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == "IDicV" ).Type;
            Debug.Assert( tIV != null );
            tIV.IsOblivious.Should().BeFalse();
            tIV.CSharpName.Should().Be( "IDictionary<object,int>" );
            tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueDictionary<object,int>" );
            tIV.ObliviousType.Should().BeSameAs( tRV );

            // IDictionary<object,int?>
            var tINV = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == "IDicNV" ).Type;
            Debug.Assert( tINV != null );
            tINV.IsOblivious.Should().BeFalse();
            tINV.CSharpName.Should().Be( "IDictionary<object,int?>" );
            tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueDictionary<object,int>" );
            tINV.ObliviousType.Should().BeSameAs( tRNV );

            ////// Dictionary of reference type (object) for the value.

            // Dictionary<int,object>: This is the oblivious.
            var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicR ) )! );
            Debug.Assert( tRR != null );
            tRR.IsOblivious.Should().BeTrue();
            tRR.CSharpName.Should().Be( "Dictionary<int,object>" );
            tRR.ImplTypeName.Should().Be( "Dictionary<int,object>" );

            // Dictionary<int,object?>
            var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNR ) )! );
            Debug.Assert( tRNR != null );
            tRNR.IsOblivious.Should().BeFalse();
            tRNR.CSharpName.Should().Be( "Dictionary<int,object?>" );
            tRNR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );
            tRNR.ObliviousType.Should().BeSameAs( tRR );

            // IDictionary<int,object?>
            var tINR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicNR ) ).Type;
            Debug.Assert( tINR != null );
            tINR.IsOblivious.Should().BeFalse();
            tINR.CSharpName.Should().Be( "IDictionary<int,object?>" );
            tINR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );
            tINR.ObliviousType.Should().BeSameAs( tRR );

            // IDictionary<int,object>
            var tIR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicR ) ).Type;
            Debug.Assert( tIR != null );
            tIR.IsOblivious.Should().BeFalse();
            tIR.CSharpName.Should().Be( "IDictionary<int,object>" );
            tIR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tIR.ObliviousType.Should().BeSameAs( tRR );

            // Dictionary of IPoco type (IVerySimplePoco) for the value.
            var n = typeof( IVerySimplePoco ).ToCSharpName();

            // Dictionary<int,IVerySimplePoco>: This is the oblivious.
            var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPR ) )! );
            Debug.Assert( tPRR != null );
            tPRR.IsOblivious.Should().BeTrue();
            tPRR.CSharpName.Should().Be( $"Dictionary<int,{n}>" );
            tPRR.ImplTypeName.Should().Be( $"Dictionary<int,{n}>" );

            // Dictionary<int,IVerySimplePoco?>
            var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPNR ) )! );
            Debug.Assert( tPRNR != null );
            tPRNR.IsOblivious.Should().BeFalse();
            tPRNR.CSharpName.Should().Be( $"Dictionary<int,{n}?>" );
            tPRNR.ImplTypeName.Should().Be( $"Dictionary<int,{n}?>" );
            tPRNR.ObliviousType.Should().BeSameAs( tPRR );

            // IDictionary<int,IVerySimplePoco?>
            var tPINR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicPNR ) ).Type;
            Debug.Assert( tPINR != null );
            tPINR.IsOblivious.Should().BeFalse();
            tPINR.CSharpName.Should().Be( $"IDictionary<int,{n}?>" );
            tPINR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoDictionary_*_*_CK" );
            tPINR.ObliviousType.Should().BeSameAs( tPRR );

            // IDictionary<int,IVerySimplePoco>
            var tPIR = (ICollectionPocoType)defPoco.Fields.Single( f => f.Name == nameof( IProperDictionaryDefinition.IDicPR ) ).Type;
            Debug.Assert( tPIR != null );
            tPIR.IsOblivious.Should().BeFalse();
            tPIR.CSharpName.Should().Be( $"IDictionary<int,{n}>" );
            tPIR.ImplTypeName.Should().Be( tPINR.ImplTypeName, "Same implementation as the Dictionary<int,IVerySimplePoco?>." );
            tPIR.ObliviousType.Should().BeSameAs( tPRR );
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

        public List<HashSet<Dictionary<int, object>>> ListCollectionO = null!;
        public (
                    List<HashSet<Dictionary<int, object?>>> C1,
                    List<HashSet<Dictionary<int, object?>?>?> C2,
                    List<HashSet<Dictionary<int, object>?>?> C3,
                    List<HashSet<Dictionary<int, object>>>? C4,
                    List<HashSet<Dictionary<int, object>>?> C5,
                    List<HashSet<Dictionary<int, object>?>> C6
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
            Debug.Assert( others != null );

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
