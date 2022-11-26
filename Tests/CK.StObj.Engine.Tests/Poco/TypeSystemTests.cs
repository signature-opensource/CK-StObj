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
using System.Xml.Linq;
using static CK.StObj.Engine.Tests.SimpleObjectsTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class TypeSystemTests
    {
        [CKTypeDefiner]
        public interface ILinkedListPart : IPoco
        {
            ILinkedListPart? Next { get; set; }
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

        public record struct RecAnonymous( int Count, string Name, (int Count, string Name) Inside );

        public RecAnonymous GetRecAnonymous => default;

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
            const int pocoTypesCount = 4 + 2; // IPoco and IClosedPoco
            const int listTypesCount = 1;
            const int anonymousTypesCount = 2 + 2; //(Count,Name) and (Count,Name,Inside) and their respective "naked" ImplNominalType.

            ts.AllTypes.Count.Should().Be( (basicTypesCount + pocoTypesCount + listTypesCount + anonymousTypesCount) * 2 );

            int before = ts.AllTypes.Count;
            var tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetRecAnonymous ) )! );
            Debug.Assert( tRec != null );
            ts.AllTypes.Count.Should().Be( before + 2 );
            tRec.Kind.Should().Be( PocoTypeKind.Record );

            IPrimaryPocoType wA = ts.GetPrimaryPocoType( typeof( IPartWithAnonymous ) )!;
            IPocoType countAndName = wA.Fields[0].Type;

            IPrimaryPocoType wR = ts.GetPrimaryPocoType( typeof( IPartWithRecAnonymous ) )!;
            ((IRecordPocoType)wR.Fields[0].Type).Fields[2].Type.Should().BeSameAs( countAndName );
        }

        // Same structure but not same field names.
        public struct SameAsRecAnonymous
        {
            public int A;
            [DefaultValue( "" )]
            public string B;
            public (int, string N) C;
        }

        public SameAsRecAnonymous GetSameNakedAsRecAnonymous => default;

        // Same field names but not same default.
        public struct NotSameAsRecAnonymous
        {
            public int A;
            [DefaultValue( "Not the default string." )]
            public string B;
            public (int, string N) C;
        }

        public NotSameAsRecAnonymous GetNotSameNakedAsRecAnonymous => default;

        [Test]
        public void NakedRecord_embedds_the_default_values()
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );

            var tRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetRecAnonymous ) )! );
            Debug.Assert( tRec != null );
            var tSameAsRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetSameNakedAsRecAnonymous ) )! );
            Debug.Assert( tSameAsRec != null );

            tSameAsRec.Should().NotBeSameAs( tRec );

            var tRecDef = tRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;
            var tSameAsRecDef = tSameAsRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tSameAsRecDef.Should().Be( "new(){B = \"\", C = (default, \"\")}" );
            tRecDef.Should().Be( "new(){Name = \"\", Inside = (default, \"\")}" );

            var rtNotSameAsRec = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetNotSameNakedAsRecAnonymous ) )! );
            Debug.Assert( rtNotSameAsRec != null );
            rtNotSameAsRec.Should().NotBeSameAs( tRec );
            var tNotSameAsRecDef = rtNotSameAsRec.DefaultValueInfo.DefaultValue!.ValueCSharpSource;

            tNotSameAsRecDef.Should().Be( "new(){B = @\"Not the default string.\", C = (default, \"\")}" );
        }

        public struct AnEmptyOne { }

        public AnEmptyOne GetAnEmptyOne => default;

        [Test]
        public void an_empty_records_is_handled_bu_is_not_exchangeable()
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );
            var tEmptyOne = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetAnEmptyOne ) )! );
            Debug.Assert( tEmptyOne != null );
            tEmptyOne.IsExchangeable.Should().BeFalse();
        }

        public (int A, string? B)? GetNullableValueTuple => default;
        public (int A, string? B) GetNonNullableValueTuple => default;

        [Test]
        public void nullable_value_tuple_nullability_information()
        {
            var f = new ExtMemberInfoFactory();
            var nullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNullableValueTuple ) )! );
            nullInfo.IsNullable.Should().BeTrue();
            nullInfo.ElementType.Should().BeNull();
            nullInfo.GenericTypeArguments.Should().HaveCount( 2 );
            nullInfo.Type.Should().Be( typeof( Nullable<(int, string)> ) );

            var nonNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNonNullableValueTuple ) )! );
            nonNullInfo.IsNullable.Should().BeFalse();
            nonNullInfo.ElementType.Should().BeNull();
            nonNullInfo.GenericTypeArguments.Should().HaveCount( 2 );
            nonNullInfo.Type.Should().Be( typeof( ValueTuple<int, string> ) );

            nullInfo.ToNullable().Should().BeSameAs( nullInfo );
            var nonNullInfo2 = nullInfo.ToNonNullable();
            nonNullInfo2.Should().BeEquivalentTo( nonNullInfo );

            nonNullInfo.ToNonNullable().Should().BeSameAs( nonNullInfo );
            var nullInfo2 = nonNullInfo.ToNullable();
            nullInfo2.Should().BeEquivalentTo( nullInfo );
        }

        public (int A, string? B)? NullableField;
        public (int A, string? B) NonNullableField;

        public ref (int A, string? B)? RefGetNullableValueTuple => ref NullableField;
        public ref (int A, string? B) RefGetNonNullableValueTuple => ref NonNullableField;

        [Test]
        public void ref_nullable_value_tuple_nullability_information()
        {
            var f = new ExtMemberInfoFactory();
            var nullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNullableValueTuple ) )! );
            nullInfo.IsNullable.Should().BeTrue();
            nullInfo.IsHomogeneous.Should().BeTrue();
            nullInfo.ElementType.Should().BeNull();
            nullInfo.GenericTypeArguments.Should().HaveCount( 2 );
            nullInfo.Type.Should().Be( typeof( Nullable<(int, string)> ) );

            var nonNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNonNullableValueTuple ) )! );
            nonNullInfo.IsNullable.Should().BeFalse();
            nonNullInfo.IsHomogeneous.Should().BeTrue();
            nonNullInfo.ElementType.Should().BeNull();
            nonNullInfo.GenericTypeArguments.Should().HaveCount( 2 );
            nonNullInfo.Type.Should().Be( typeof( ValueTuple<int, string> ) );

            nullInfo.ToNullable().Should().BeSameAs( nullInfo );
            var nonNullInfo2 = nullInfo.ToNonNullable();
            nonNullInfo2.Should().BeEquivalentTo( nonNullInfo );

            nonNullInfo.ToNonNullable().Should().BeSameAs( nonNullInfo );
            var nullInfo2 = nonNullInfo.ToNullable();
            nullInfo2.Should().BeEquivalentTo( nullInfo );
        }

        [Test]
        public void field_nullable_value_tuple_nullability_information()
        {
            var f = new ExtMemberInfoFactory();
            var nullInfo = f.CreateNullabilityInfo( GetType().GetField( nameof( NullableField ) )! );
            nullInfo.IsNullable.Should().BeTrue();
            nullInfo.IsHomogeneous.Should().BeTrue();
            nullInfo.ElementType.Should().BeNull();
            nullInfo.GenericTypeArguments.Should().HaveCount( 2 );
            nullInfo.Type.Should().Be( typeof( Nullable<(int, string)> ) );

            var nonNullInfo = f.CreateNullabilityInfo( GetType().GetField( nameof( NonNullableField ) )! );
            nonNullInfo.IsNullable.Should().BeFalse();
            nonNullInfo.IsHomogeneous.Should().BeTrue();
            nonNullInfo.ElementType.Should().BeNull();
            nonNullInfo.GenericTypeArguments.Should().HaveCount( 2 );
            nonNullInfo.Type.Should().Be( typeof( ValueTuple<int, string> ) );

            nullInfo.ToNullable().Should().BeSameAs( nullInfo );
            var nonNullInfo2 = nullInfo.ToNonNullable();
            nonNullInfo2.Should().BeEquivalentTo( nonNullInfo );

            nonNullInfo.ToNonNullable().Should().BeSameAs( nonNullInfo );
            var nullInfo2 = nonNullInfo.ToNullable();
            nullInfo2.Should().BeEquivalentTo( nullInfo );
        }

        [DisallowNull]
        public ref (int A, string? B)? RefGetNullableValueTupleHeterogeneous => ref NullableField;
        [DisallowNull]
        public (int A, string? B)? GetNullableValueTupleHeterogeneous { get; set; }

        [AllowNull]
        public ref (int A, string? B) RefGetNonNullableValueTupleHeterogeneous => ref NonNullableField;
        [AllowNull]
        public (int A, string? B) GetNonNullableValueTupleHeterogeneous { get; set; }

        [DisallowNull]
        public (int A, string? B)? NullableValueTupleHeterogeneousField;
        [AllowNull]
        public (int A, string? B) NonNullableValueTupleHeterogeneousField;

        [Test]
        public void homogeneous_nullability_detection()
        {
            var f = new ExtMemberInfoFactory();

            // ref properties always use the ReadState: homogeneity is by design.
            var rNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNullableValueTupleHeterogeneous ) )! );
            rNullInfo.IsNullable.Should().Be( true );
            rNullInfo.IsHomogeneous.Should().Be( true );
            rNullInfo.ReflectsReadState.Should().Be( true );
            rNullInfo.ReflectsWriteState.Should().Be( true );

            var rNonNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNonNullableValueTupleHeterogeneous ) )!, useReadState: false );
            rNonNullInfoW.IsNullable.Should().Be( false );
            rNonNullInfoW.IsHomogeneous.Should().Be( true );
            rNonNullInfoW.ReflectsReadState.Should().Be( true );
            rNonNullInfoW.ReflectsWriteState.Should().Be( true );

            // Fields are like ref properties: homogeneity is by design.
            var fNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNullableValueTupleHeterogeneous ) )! );
            fNullInfo.IsNullable.Should().Be( true );
            fNullInfo.IsHomogeneous.Should().Be( true );
            fNullInfo.ReflectsReadState.Should().Be( true );
            fNullInfo.ReflectsWriteState.Should().Be( true );

            var fNonNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNonNullableValueTupleHeterogeneous ) )!, useReadState: false );
            fNonNullInfoW.IsNullable.Should().Be( false );
            fNonNullInfoW.IsHomogeneous.Should().Be( true );
            fNonNullInfoW.ReflectsReadState.Should().Be( true );
            fNonNullInfoW.ReflectsWriteState.Should().Be( true );

            // Regular properties can be heterogeneous if and only if the type is nullable.
            var vNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNullableValueTupleHeterogeneous ) )! );
            vNullInfo.IsNullable.Should().Be( true );
            vNullInfo.IsHomogeneous.Should().Be( false );
            vNullInfo.ReflectsReadState.Should().Be( true );
            vNullInfo.ReflectsWriteState.Should().Be( false );
            var vNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNullableValueTupleHeterogeneous ) )!, useReadState: false );
            vNullInfoW.IsNullable.Should().Be( false );
            vNullInfoW.IsHomogeneous.Should().Be( false );
            vNullInfoW.ReflectsReadState.Should().Be( false );
            vNullInfoW.ReflectsWriteState.Should().Be( true );

            // Error CS0037  Cannot convert null to '(int A, string? B)' because it is a non - nullable value type.
            // GetNonNullableValueTupleHeterogeneous = null;
            //
            // This is correctly handled: [AllowNull] is ignored.
            var vNonNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNonNullableValueTupleHeterogeneous ) )!, useReadState: false );
            vNonNullInfoW.IsNullable.Should().Be( false );
            vNonNullInfoW.IsHomogeneous.Should().Be( true );
            vNonNullInfoW.ReflectsReadState.Should().Be( true );
            vNonNullInfoW.ReflectsWriteState.Should().Be( true );
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

        [Test]
        public void nominal_and_type_name_List()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

            // List of Value type

            // List<int>: This is a nominal type.
            var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListV ) )! );
            Debug.Assert( tRV != null && tRV.ImplNominalType == tRV );
            tRV.CSharpName.Should().Be( "List<int>" );
            tRV.ImplTypeName.Should().Be( "List<int>" );

            // List<int?>: : This is a nominal type.
            var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNV ) )! );
            Debug.Assert( tRNV != null && tRNV.ImplNominalType == tRNV );
            tRNV.CSharpName.Should().Be( "List<int?>" );
            tRNV.ImplTypeName.Should().Be( "List<int?>" );

            // IList<int>
            var tIV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListV ) )! );
            Debug.Assert( tIV != null );
            tIV.CSharpName.Should().Be( "IList<int>" );
            tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueList<int>" );
            tIV.ImplNominalType.Should().BeSameAs( tRV );

            // IList<int?>
            var tINV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListNV ) )! );
            Debug.Assert( tINV != null );
            tINV.CSharpName.Should().Be( "IList<int?>" );
            tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueList<int>" );
            tINV.ImplNominalType.Should().BeSameAs( tRNV );

            // List of Reference type (object)

            // List<object?>: This is the nominal type.
            var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListNR ) )! );
            Debug.Assert( tRNR != null && tRNR.ImplNominalType == tRNR );
            tRNR.CSharpName.Should().Be( "List<object?>" );
            tRNR.ImplTypeName.Should().Be( "List<object?>" );

            // List<object>
            var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListR ) )! );
            Debug.Assert( tRR != null );
            tRR.CSharpName.Should().Be( "List<object>" );
            tRR.ImplTypeName.Should().Be( "List<object?>" );
            tRR.ImplNominalType.Should().BeSameAs( tRNR );

            // IList<object?>
            var tINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListNR ) )! );
            Debug.Assert( tINR != null );
            tINR.CSharpName.Should().Be( "IList<object?>" );
            tINR.ImplTypeName.Should().Be( "List<object?>" );
            tINR.ImplNominalType.Should().BeSameAs( tRNR );

            // IList<object>
            var tIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListR ) )! );
            Debug.Assert( tIR != null );
            tIR.CSharpName.Should().Be( "IList<object>" );
            tIR.ImplTypeName.Should().Be( "List<object?>" );
            tIR.ImplNominalType.Should().BeSameAs( tRNR );

            // List of Reference type but IVerySimplePoco.
            var n = typeof( IVerySimplePoco ).ToCSharpName();

            // List<IVerySimplePoco?>: This is the nominal implementation.
            var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPNR ) )! );
            Debug.Assert( tPRNR != null && tPRNR.ImplNominalType == tPRNR );
            tPRNR.CSharpName.Should().Be( $"List<{n}?>" );
            tPRNR.ImplTypeName.Should().Be( $"List<{n}?>" );

            // List<IVerySimplePoco>
            var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( ListPR ) )! );
            Debug.Assert( tPRR != null );
            tPRR.CSharpName.Should().Be( $"List<{n}>" );
            tPRR.ImplTypeName.Should().Be( $"List<{n}?>" );
            tPRR.ImplNominalType.Should().BeSameAs( tPRNR );

            // IList<IVerySimplePoco?>
            var tPINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListPNR ) )! );
            Debug.Assert( tPINR != null );
            tPINR.CSharpName.Should().Be( $"IList<{n}?>" );
            tPINR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoList_*_CK" );
            tPINR.ImplNominalType.Should().BeSameAs( tPRNR );

            // IList<IVerySimplePoco>
            var tPIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IListPR ) )! );
            Debug.Assert( tPIR != null );
            tPIR.CSharpName.Should().Be( $"IList<{n}>" );
            tPIR.ImplTypeName.Should().Be( tPINR.ImplTypeName, "Same implementation as the IList<IVerySimplePoco?>." );
            tPIR.ImplNominalType.Should().BeSameAs( tPRNR );

        }

        public Dictionary<object,int?> DicNV = null!;
        public Dictionary<object, int> DicV = null!;
        public IDictionary<object, int?> IDicNV = null!;
        public IDictionary<object, int> IDicV = null!;

        public Dictionary<int, object?> DicNR = null!;
        public Dictionary<int, object> DicR = null!;
        public IDictionary<int, object?> IDicNR = null!;
        public IDictionary<int, object> IDicR = null!;


        public Dictionary<int,IVerySimplePoco?> DicPNR = null!;
        public Dictionary<int,IVerySimplePoco> DicPR = null!;
        public IDictionary<int, IVerySimplePoco?> IDicPNR = null!;
        public IDictionary<int, IVerySimplePoco> IDicPR = null!;

        [Test]
        public void nominal_and_type_name_Dictionary()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IVerySimplePoco ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.CKTypeResult.PocoTypeSystem;

            // Dictionary of Value type for the value (int)

            // Dictionary<object,int>: This is a nominal implementation.
            var tRV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicV ) )! );
            Debug.Assert( tRV != null && tRV.ImplNominalType == tRV );
            tRV.CSharpName.Should().Be( "Dictionary<object,int>" );
            tRV.ImplTypeName.Should().Be( "Dictionary<object,int>" );

            // Dictionary<object,int?>: This is a nominal implementation.
            var tRNV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNV ) )! );
            Debug.Assert( tRNV != null && tRNV.ImplTypeName == tRNV.ImplNominalType.ImplTypeName );
            tRNV.CSharpName.Should().Be( "Dictionary<object,int?>" );
            tRNV.ImplTypeName.Should().Be( "Dictionary<object,int?>" );

            // IDictionary<object,int>
            var tIV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicV ) )! );
            Debug.Assert( tIV != null );
            tIV.CSharpName.Should().Be( "IDictionary<object,int>" );
            tIV.ImplTypeName.Should().Be( "CovariantHelpers.CovNotNullValueDictionary<object,int>" );
            tIV.ImplNominalType.Should().BeSameAs( tRV );

            // IDictionary<object,int?>
            var tINV = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicNV ) )! );
            Debug.Assert( tINV != null );
            tINV.CSharpName.Should().Be( "IDictionary<object,int?>" );
            tINV.ImplTypeName.Should().Be( "CovariantHelpers.CovNullableValueDictionary<object,int>" );
            tINV.ImplNominalType.Should().BeSameAs( tRNV );

            // Dictionary of reference type (object) for the value.

            // Dictionary<int,object?>: This is the nominal type.
            var tRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicNR ) )! );
            Debug.Assert( tRNR != null && tRNR.ImplNominalType == tRNR );
            tRNR.CSharpName.Should().Be( "Dictionary<int,object?>" );
            tRNR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );

            // Dictionary<int,object>
            var tRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicR ) )! );
            Debug.Assert( tRR != null );
            tRR.CSharpName.Should().Be( "Dictionary<int,object>" );
            tRR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );
            tRR.ImplNominalType.Should().BeSameAs( tRNR );

            // IDictionary<int,object?>
            var tINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicNR ) )! );
            Debug.Assert( tINR != null );
            tINR.CSharpName.Should().Be( "IDictionary<int,object?>" );
            tINR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );
            tINR.ImplNominalType.Should().BeSameAs( tRNR );

            // IDictionary<int,object>
            var tIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicR ) )! );
            Debug.Assert( tIR != null );
            tIR.CSharpName.Should().Be( "IDictionary<int,object>" );
            tIR.ImplTypeName.Should().Be( "Dictionary<int,object?>" );
            tIR.ImplNominalType.Should().BeSameAs( tRNR );

            // Dictionary of IPoco type (IVerySimplePoco) for the value.
            var n = typeof( IVerySimplePoco ).ToCSharpName();

            // Dictionary<int,IVerySimplePoco?>: This is the nominal type.
            var tPRNR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPNR ) )! );
            Debug.Assert( tPRNR != null && tPRNR.ImplTypeName == tPRNR.ImplNominalType.ImplTypeName );
            tPRNR.CSharpName.Should().Be( $"Dictionary<int,{n}?>" );
            tPRNR.ImplTypeName.Should().Be( $"Dictionary<int,{n}?>" );

            // Dictionary<int,IVerySimplePoco>
            var tPRR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( DicPR ) )! );
            Debug.Assert( tPRR != null );
            tPRR.CSharpName.Should().Be( $"Dictionary<int,{n}>" );
            tPRR.ImplTypeName.Should().Be( $"Dictionary<int,{n}?>" );
            tPRR.ImplNominalType.Should().BeSameAs( tPRNR );

            // IDictionary<int,IVerySimplePoco?>
            var tPINR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicPNR ) )! );
            Debug.Assert( tPINR != null );
            tPINR.CSharpName.Should().Be( $"IDictionary<int,{n}?>" );
            tPINR.ImplTypeName.Should().MatchEquivalentOf( "CK.GRSupport.PocoDictionary_*_*_CK" );
            tPINR.ImplNominalType.Should().BeSameAs( tPRNR );

            // IDictionary<int,IVerySimplePoco>
            var tPIR = (ICollectionPocoType?)ts.Register( TestHelper.Monitor, GetType().GetField( nameof( IDicPR ) )! );
            Debug.Assert( tPIR != null );
            tPIR.CSharpName.Should().Be( $"IDictionary<int,{n}>" );
            tPIR.ImplTypeName.Should().Be( tPINR.ImplTypeName, "Same implementation as the Dictionary<int,IVerySimplePoco?>." );
            tPIR.ImplNominalType.Should().BeSameAs( tPRNR );
        }

    }

}
