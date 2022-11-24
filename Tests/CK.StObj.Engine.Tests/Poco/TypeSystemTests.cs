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
            const int anonymousTypesCount = 2; //(Count,Name) and (Count,Name,Inside)

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
        public void an_empty_records_is_handled()
        {
            var ts = new PocoTypeSystem( new ExtMemberInfoFactory() );
            var tEmptyOne = ts.Register( TestHelper.Monitor, GetType().GetProperty( nameof( GetAnEmptyOne ) )! );
            Debug.Assert( tEmptyOne != null );
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

    }

}
