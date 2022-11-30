using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;
using static CK.StObj.Engine.Tests.SimpleObjectsTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class TypeSystemTests
    {
        public enum AnEnum { Value }

        [CKTypeDefiner]
        public interface ILinkedListPart : IPoco
        {
            ILinkedListPart? Next { get; set; }

            AnEnum AnEnum { get; set; }
        }

        public interface IPartWithAnonymous : ILinkedListPart
        {
            ref (int Count, string Name) Anonymous { get; }
        }

        public interface IPartWithRecAnonymous : ILinkedListPart
        {
            ref (int Count, string Name, (int Count, string Name) Inside) RecAnonymous { get; }
        }

        public interface IWithList : ILinkedListPart
        {
            List<(int Count, string Name)> Thing { get; }
        }

        public record struct NamedRec( int Count, string Name, (int Count, string Name) Inside );

        public NamedRec GetNamedRec => default;

        [Test]
        public void AllTypes_and_identity_test()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ILinkedListPart ),
                                                     typeof( IPartWithAnonymous ),
                                                     typeof( IPartWithRecAnonymous ),
                                                     typeof( IWithList ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

            const int basicTypesCount = 19;
            const int enumTypesCount = 1; // AnEnum
            const int pocoTypesCount = 4 + 2; // IPoco and IClosedPoco
            const int listTypesCount = 1 + 1; // List<(int,string)> and List<(int Count, string Name)>
            const int anonymousTypesCount = 2 + 2; //(Count,Name) and (Count,Name,Inside) and their respective oblivious types.

            ts.AllTypes.Count.Should().Be( (basicTypesCount + enumTypesCount + pocoTypesCount + listTypesCount + anonymousTypesCount) * 2 );

            int before = ts.AllTypes.Count;
            var tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNamedRec ) )! );
            Debug.Assert( tRec != null );
            ts.AllTypes.Count.Should().Be( before + 2 );
            tRec.Kind.Should().Be( PocoTypeKind.Record );

            IPrimaryPocoType wA = ts.GetPrimaryPocoType( typeof( IPartWithAnonymous ) )!;
            IPocoType countAndName = wA.Fields[0].Type;

            IPrimaryPocoType wR = ts.GetPrimaryPocoType( typeof( IPartWithRecAnonymous ) )!;
            ((IRecordPocoType)wR.Fields[0].Type).Fields[2].Type.Should().BeSameAs( countAndName );
        }

        // Same structure but not same field names.
        public struct NotSameFieldNameAsNamedRec
        {
            public int A;
            [DefaultValue( "" )]
            public string B;
            public (int, string N) C;
        }

        public NotSameFieldNameAsNamedRec GetNotSameFieldNameAsNamedRec => default;

        // Same field names but not same default.
        public struct NotSameDefaultAsNamedRec
        {
            public int A;
            [DefaultValue( "Not the default string." )]
            public string B;
            public (int, string N) C;
        }

        public NotSameDefaultAsNamedRec GetNotSameDefaultAsNamedRec => default;

        [Test]
        public void named_record_embedds_the_default_values()
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );

            var tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNamedRec ) )! );
            Debug.Assert( tRec != null );
            var tNotFieldName = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameFieldNameAsNamedRec ) )! );
            Debug.Assert( tNotFieldName != null );

            tNotFieldName.Should().NotBeSameAs( tRec );

            var tRecDef = tRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;
            var tNotFieldNameDef = tNotFieldName.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tRecDef.Should().Be( "new(){Name = \"\", Inside = (default, \"\")}" );
            tNotFieldNameDef.Should().Be( "new(){B = \"\", C = (default, \"\")}" );

            var tNotSameDefault = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameDefaultAsNamedRec ) )! );
            Debug.Assert( tNotSameDefault != null );
            tNotSameDefault.Should().NotBeSameAs( tRec );
            var tNotSameDefaultDef = tNotSameDefault.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tNotSameDefaultDef.Should().Be( "new(){B = @\"Not the default string.\", C = (default, \"\")}" );
        }

        public struct AnEmptyOne { }

        public AnEmptyOne GetAnEmptyOne => default;

        [Test]
        public void an_empty_records_is_handled_but_is_not_exchangeable()
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );
            var tEmptyOne = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetAnEmptyOne ) )! );
            Debug.Assert( tEmptyOne != null );
            tEmptyOne.IsExchangeable.Should().BeFalse();
        }


        [ExternalName( "IVerySimplePoco" )]
        public interface IVerySimplePoco : IPoco
        {
            int Value { get; set; }
        }

        public List<int?> ListNV = null!;
        public List<int> ListV = null!;
        public IList<int?> IListNV = null!;
        public IList<int> IListV = null!;

        public List<object?> ListNR = null!;
        public List<object> ListR = null!;
        public IList<object?> IListNR = null!;
        public IList<object> IListR = null!;

        public List<IVerySimplePoco?> ListPNR = null!;
        public List<IVerySimplePoco> ListPR = null!;
        public IList<IVerySimplePoco?> IListPNR = null!;
        public IList<IVerySimplePoco> IListPR = null!;

        [TestCase( false )]
        [TestCase( true )]
        public void oblivious_List( bool revert )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

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
                // List<object?>: This is the oblivious type.
                ICollectionPocoType? tRNR = List_ObjectN_IsOblivious( ts );
                // List<object>
                ICollectionPocoType? tRR = List_Object( revert, ts, tRNR );
                // IList<object?>
                ICollectionPocoType? tINR = IList_ObjectN( revert, ts, tRNR );
                // IList<object>
                ICollectionPocoType? tIR = IList_Object( revert, ts, tRNR );

                // List of Reference type but IVerySimplePoco.
                var n = typeof( IVerySimplePoco ).ToCSharpName();
                // List<IVerySimplePoco?>: This is the oblivious implementation.
                ICollectionPocoType? tPRNR = List_PocoN_IsOblivious( ts, n );
                // List<IVerySimplePoco>
                ICollectionPocoType? tPRR = List_Poco( revert, ts, n, tPRNR );
                // IList<IVerySimplePoco?>
                ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n, tPRNR );
                // IList<IVerySimplePoco>
                ICollectionPocoType? tPIR = IList_Poco( revert, ts, n, tPRNR, tPINR );
            }
            else
            {
                // List of Reference type but IVerySimplePoco.
                var n = typeof( IVerySimplePoco ).ToCSharpName();
                // IList<IVerySimplePoco>
                ICollectionPocoType? tPIR = IList_Poco( revert, ts, n, null!, null! );
                // IList<IVerySimplePoco?>
                ICollectionPocoType? tPINR = IList_PocoN( revert, ts, n, null! );
                // List<IVerySimplePoco>
                ICollectionPocoType? tPRR = List_Poco( revert, ts, n, null! );
                // List<IVerySimplePoco?>: This is the oblivious implementation.
                ICollectionPocoType? tPRNR = List_PocoN_IsOblivious( ts, n );

                // List of Reference type (object)
                // IList<object>
                ICollectionPocoType? tIR = IList_Object( revert, ts, null! );
                // IList<object?>
                ICollectionPocoType? tINR = IList_ObjectN( revert, ts, null! );
                // List<object>
                ICollectionPocoType? tRR = List_Object( revert, ts, null! );
                // List<object?>: This is the oblivious type.
                ICollectionPocoType? tRNR = List_ObjectN_IsOblivious( ts );

                // List of Value type
                // IList<int?>
                ICollectionPocoType? tINV = IList_IntN( revert, ts, null! );
                // IList<int>
                ICollectionPocoType? tIV = IList_Int( revert, ts, null! );
                // List<int?>: : This is a oblivious type.
                ICollectionPocoType? tRNV = List_IntN_IsOblivious( ts );
                // List<int>: This is a oblivious type.
                ICollectionPocoType? tRV = List_Int_IsOblivious( ts );

                tIV.ObliviousType.Should().BeSameAs( tRV.Nullable );
                tINV.ObliviousType.Should().BeSameAs( tRNV.Nullable );
                tRR.ObliviousType.Should().BeSameAs( tRNR.Nullable );
                tINR.ObliviousType.Should().BeSameAs( tRNR.Nullable );
                tIR.ObliviousType.Should().BeSameAs( tRNR.Nullable );
                tPRR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
                tPINR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
                tPIR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
            }

            ICollectionPocoType List_Int_IsOblivious( IPocoTypeSystem ts )
            {
                var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListV ) )! );
                Debug.Assert( tRV != null && tRV.ObliviousType == tRV.Nullable && tRV.Nullable.IsOblivious );
                tRV.CSharpName.Should().Be( "List<int>" );
                tRV.ImplTypeName.Should().Be( "List<int>" );
                return tRV;
            }

            ICollectionPocoType List_IntN_IsOblivious( IPocoTypeSystem ts )
            {
                var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNV ) )! );
                Debug.Assert( tRNV != null && tRNV.ObliviousType == tRNV.Nullable && tRNV.ObliviousType.Nullable == tRNV.Nullable );
                tRNV.CSharpName.Should().Be( "List<int?>" );
                tRNV.ImplTypeName.Should().Be( "List<int?>" );
                return tRNV;
            }

            ICollectionPocoType IList_Int( bool revert, IPocoTypeSystem ts, ICollectionPocoType tRV )
            {
                var tIV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListV ) )! );
                Debug.Assert( tIV != null );
                tIV.CSharpName.Should().Be( "IList<int>" );
                tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueList<int>" );
                if( !revert ) tIV.ObliviousType.Should().BeSameAs( tRV.Nullable );
                return tIV;
            }

            ICollectionPocoType IList_IntN( bool revert, IPocoTypeSystem ts, ICollectionPocoType tRNV )
            {
                var tINV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListNV ) )! );
                Debug.Assert( tINV != null );
                tINV.CSharpName.Should().Be( "IList<int?>" );
                tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueList<int>" );
                if( !revert ) tINV.ObliviousType.Should().BeSameAs( tRNV.Nullable );
                return tINV;
            }

            ICollectionPocoType List_ObjectN_IsOblivious( IPocoTypeSystem ts )
            {
                var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNR ) )! );
                Debug.Assert( tRNR != null && tRNR.ObliviousType == tRNR.Nullable && tRNR.Nullable.IsOblivious );
                tRNR.CSharpName.Should().Be( "List<object?>" );
                tRNR.ImplTypeName.Should().Be( "List<object?>" );
                return tRNR;
            }

            ICollectionPocoType List_Object( bool revert, IPocoTypeSystem ts, ICollectionPocoType tRNR )
            {
                var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListR ) )! );
                Debug.Assert( tRR != null );
                tRR.CSharpName.Should().Be( "List<object>" );
                tRR.ImplTypeName.Should().Be( "List<object>" );
                if( !revert ) tRR.ObliviousType.Should().BeSameAs( tRNR.Nullable );
                return tRR;
            }

            ICollectionPocoType IList_ObjectN( bool revert, IPocoTypeSystem ts, ICollectionPocoType tRNR )
            {
                var tINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListNR ) )! );
                Debug.Assert( tINR != null );
                tINR.CSharpName.Should().Be( "IList<object?>" );
                tINR.ImplTypeName.Should().Be( "List<object?>" );
                if( !revert ) tINR.ObliviousType.Should().BeSameAs( tRNR.Nullable );
                return tINR;
            }

            ICollectionPocoType IList_Object( bool revert, IPocoTypeSystem ts, ICollectionPocoType tRNR )
            {
                var tIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListR ) )! );
                Debug.Assert( tIR != null );
                tIR.CSharpName.Should().Be( "IList<object>" );
                tIR.ImplTypeName.Should().Be( "List<object>" );
                if( !revert ) tIR.ObliviousType.Should().BeSameAs( tRNR.Nullable );
                return tIR;
            }

            ICollectionPocoType List_PocoN_IsOblivious( IPocoTypeSystem ts, string n )
            {
                var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPNR ) )! );
                Debug.Assert( tPRNR != null && tPRNR.ObliviousType == tPRNR.Nullable && tPRNR.Nullable.IsOblivious );
                tPRNR.CSharpName.Should().Be( $"List<{n}?>" );
                tPRNR.ImplTypeName.Should().Be( $"List<{n}?>" );
                return tPRNR;
            }

            ICollectionPocoType List_Poco( bool revert, IPocoTypeSystem ts, string n, ICollectionPocoType tPRNR )
            {
                var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPR ) )! );
                Debug.Assert( tPRR != null );
                tPRR.CSharpName.Should().Be( $"List<{n}>" );
                tPRR.ImplTypeName.Should().Be( $"List<{n}>" );
                if( !revert ) tPRR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
                return tPRR;
            }

            ICollectionPocoType IList_PocoN( bool revert, IPocoTypeSystem ts, string n, ICollectionPocoType tPRNR )
            {
                var tPINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListPNR ) )! );
                Debug.Assert( tPINR != null );
                tPINR.CSharpName.Should().Be( $"IList<{n}?>" );
                tPINR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoList_*_CK" );
                if( !revert ) tPINR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
                return tPINR;
            }

            ICollectionPocoType IList_Poco( bool revert, IPocoTypeSystem ts, string n, ICollectionPocoType tPRNR, ICollectionPocoType tPINR )
            {
                var tPIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListPR ) )! );
                Debug.Assert( tPIR != null );
                tPIR.CSharpName.Should().Be( $"IList<{n}>" );
                if( !revert )
                {
                    tPIR.ImplTypeName.Should().Be( tPINR.ImplTypeName, "Same implementation as the IList<IVerySimplePoco?>." );
                    tPIR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
                }
                return tPIR;
            }
        }

        public Dictionary<object, int?> DicNV = null!;
        public Dictionary<object, int> DicV = null!;
        public IDictionary<object, int?> IDicNV = null!;
        public IDictionary<object, int> IDicV = null!;

        public Dictionary<int, object?> DicNR = null!;
        public Dictionary<int, object> DicR = null!;
        public IDictionary<int, object?> IDicNR = null!;
        public IDictionary<int, object> IDicR = null!;


        public Dictionary<int, IVerySimplePoco?> DicPNR = null!;
        public Dictionary<int, IVerySimplePoco> DicPR = null!;
        public IDictionary<int, IVerySimplePoco?> IDicPNR = null!;
        public IDictionary<int, IVerySimplePoco> IDicPR = null!;

        [Test]
        public void oblivious_Dictionary()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

            // Dictionary of Value type for the value (int)

            // Dictionary<object,int>: This is a nominal implementation.
            var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicV ) )! );
            Debug.Assert( tRV != null && tRV.ObliviousType == tRV.Nullable && tRV.Nullable.IsOblivious );
            tRV.CSharpName.Should().Be( "Dictionary<object,int>" );
            tRV.ImplTypeName.Should().Be( "Dictionary<object,int>" );

            // Dictionary<object,int?>: This is a nominal implementation.
            var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNV ) )! );
            Debug.Assert( tRNV != null && tRNV.ObliviousType == tRNV.Nullable && tRNV.Nullable.IsOblivious );
            tRNV.CSharpName.Should().Be( "Dictionary<object,int?>" );
            tRNV.ImplTypeName.Should().Be( "Dictionary<object,int?>" );

            // IDictionary<object,int>
            var tIV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicV ) )! );
            Debug.Assert( tIV != null );
            tIV.CSharpName.Should().Be( "IDictionary<object,int>" );
            tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueDictionary<object,int>" );
            tIV.ObliviousType.Should().BeSameAs( tRV.Nullable );

            // IDictionary<object,int?>
            var tINV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicNV ) )! );
            Debug.Assert( tINV != null );
            tINV.CSharpName.Should().Be( "IDictionary<object,int?>" );
            tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueDictionary<object,int>" );
            tINV.ObliviousType.Should().BeSameAs( tRNV.Nullable );

            // Dictionary of reference type (object) for the value.

            // Dictionary<int,object?>: This is the nominal type.
            var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNR ) )! );
            Debug.Assert( tRNR != null && tRNR.ObliviousType == tRNR.Nullable && tRNR.Nullable.IsOblivious );
            tRNR.CSharpName.Should().Be( "Dictionary<int,object?>" );
            tRNR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );

            // Dictionary<int,object>
            var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicR ) )! );
            Debug.Assert( tRR != null );
            tRR.CSharpName.Should().Be( "Dictionary<int,object>" );
            tRR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tRR.ObliviousType.Should().BeSameAs( tRNR.Nullable );

            // IDictionary<int,object?>
            var tINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicNR ) )! );
            Debug.Assert( tINR != null );
            tINR.CSharpName.Should().Be( "IDictionary<int,object?>" );
            tINR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );
            tINR.ObliviousType.Should().BeSameAs( tRNR.Nullable );

            // IDictionary<int,object>
            var tIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicR ) )! );
            Debug.Assert( tIR != null );
            tIR.CSharpName.Should().Be( "IDictionary<int,object>" );
            tIR.ImplTypeName.Should().Be( "Dictionary<int,object>" );
            tIR.ObliviousType.Should().BeSameAs( tRNR.Nullable );

            // Dictionary of IPoco type (IVerySimplePoco) for the value.
            var n = typeof( IVerySimplePoco ).ToCSharpName();

            // Dictionary<int,IVerySimplePoco?>: This is the nominal type.
            var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPNR ) )! );
            Debug.Assert( tPRNR != null && tPRNR.ObliviousType == tPRNR.Nullable && tPRNR.Nullable.IsOblivious );
            tPRNR.CSharpName.Should().Be( $"Dictionary<int,{n}?>" );
            tPRNR.ImplTypeName.Should().Be( $"Dictionary<int,{n}?>" );

            // Dictionary<int,IVerySimplePoco>
            var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPR ) )! );
            Debug.Assert( tPRR != null );
            tPRR.CSharpName.Should().Be( $"Dictionary<int,{n}>" );
            tPRR.ImplTypeName.Should().Be( $"Dictionary<int,{n}>" );
            tPRR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );

            // IDictionary<int,IVerySimplePoco?>
            var tPINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicPNR ) )! );
            Debug.Assert( tPINR != null );
            tPINR.CSharpName.Should().Be( $"IDictionary<int,{n}?>" );
            tPINR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoDictionary_*_*_CK" );
            tPINR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );

            // IDictionary<int,IVerySimplePoco>
            var tPIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicPR ) )! );
            Debug.Assert( tPIR != null );
            tPIR.CSharpName.Should().Be( $"IDictionary<int,{n}>" );
            tPIR.ImplTypeName.Should().Be( tPINR.ImplTypeName, "Same implementation as the Dictionary<int,IVerySimplePoco?>." );
            tPIR.ObliviousType.Should().BeSameAs( tPRNR.Nullable );
        }

        // AnonymousSampleO is the common ObliviousType: 
        public (IVerySimplePoco?, List<IVerySimplePoco?>?) AnonymousSampleO = default;
        public (IVerySimplePoco A, IList<IVerySimplePoco> B) AnonymousSample1 = default;
        public (IVerySimplePoco? A, IList<IVerySimplePoco?> B) AnonymousSample2 = default;
        public (IVerySimplePoco? A, IList<IVerySimplePoco?>? B) AnonymousSample3 = default;
        public (IVerySimplePoco? A, IList<IVerySimplePoco> B) AnonymousSample4 = default;
        public (IVerySimplePoco? A, IList<IVerySimplePoco> B2) AnonymousSample5 = default;

        [TestCase( "ObliviousFirst" )]
        [TestCase( "ObliviousLast" )]
        public void oblivious_anonymous_record( string mode )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

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
            var t5 = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( AnonymousSample5 ) )! );
            Debug.Assert( t5 != null );

            if( mode == "ObliviousLast" ) tO = (IRecordPocoType?)ts.FindObliviousType( GetType().GetField( nameof( AnonymousSampleO ) )!.FieldType );
            Debug.Assert( tO != null && tO.IsAnonymous && tO.IsOblivious );

            tO.ObliviousType.Should().BeSameAs( tO );
            t1.ObliviousType.Should().BeSameAs( tO );
            t2.ObliviousType.Should().BeSameAs( tO );
            t3.ObliviousType.Should().BeSameAs( tO );
            t4.ObliviousType.Should().BeSameAs( tO );
            t5.ObliviousType.Should().BeSameAs( tO );
            new object[] { tO, t1, t2, t3, t4, t5 }.Distinct().Should().HaveCount( 6, "Different PocoTypes." );
        }

        public List<HashSet<Dictionary<int, object?>?>?> ListCollectionO = null!;
        public (
                    List<HashSet<Dictionary<int, object?>>> C1,
                    IList<HashSet<Dictionary<int, object?>?>?> C2,
                    List<ISet<Dictionary<int, object>?>?> C3,
                    List<HashSet<IDictionary<int, object>?>?> C4,
                    List<HashSet<IDictionary<int, object>>> C5,
                    List<HashSet<Dictionary<int, object>?>?> C6,
                    List<HashSet<Dictionary<int, object>?>?> C7,
                    List<HashSet<IDictionary<int, object>?>?> C8,
                    List<ISet<Dictionary<int, object?>?>?> C9,
                    IList<ISet<IDictionary<int, object>>> C10
               )
               ListCollections = default!;

        [TestCase( "ObliviousFirst" )]
        [TestCase( "ObliviousLast" )]
        public void oblivious_anonymous_record_and_collections( string mode )
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );

            ICollectionPocoType? tO = null;
            if( mode == "ObliviousFirst" ) tO = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollectionO ) )! )!.Nullable;

            IRecordPocoType? others = (IRecordPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollections ) )! );
            Debug.Assert( others != null );

            if( mode == "ObliviousLast" ) tO = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListCollectionO ) )! )!.Nullable;

            var culprits = others.Fields.Select( f => f.Type ).Where( t => t.ObliviousType != tO );
            culprits.Should().BeEmpty( "All these collections have the same oblivious type." );

            var oA = others.ObliviousType;
            oA.IsOblivious.Should().BeTrue();
            oA.Fields.Where( f => !f.IsUnnamed ).Should().BeEmpty( "The oblivious anonymous record has no field name." );
            oA.Fields.Where( f => f.Type != tO ).Should().BeEmpty( "The oblivious anonymous record has oblivious field types." );
        }
    }

}
