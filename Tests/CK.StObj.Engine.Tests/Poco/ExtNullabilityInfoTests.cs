using CK.Setup;
using Shouldly;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CA1822 // Mark members as static

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class ExtNullabilityInfoTests
{
    public (int A, string? B)? GetNullableValueTuple => default;
    public (int A, string? B) GetNonNullableValueTuple => default;

    [Test]
    public void nullable_value_tuple_nullability_information()
    {
        var f = new ExtMemberInfoFactory();
        var nullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNullableValueTuple ) )! );
        nullInfo.IsNullable.ShouldBeTrue();
        nullInfo.ElementType.ShouldBeNull();
        nullInfo.GenericTypeArguments.Count.ShouldBe( 2 );
        nullInfo.Type.ShouldBe( typeof( Nullable<(int, string)> ) );

        var nonNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNonNullableValueTuple ) )! );
        nonNullInfo.IsNullable.ShouldBeFalse();
        nonNullInfo.ElementType.ShouldBeNull();
        nonNullInfo.GenericTypeArguments.Count.ShouldBe( 2 );
        nonNullInfo.Type.ShouldBe( typeof( ValueTuple<int, string> ) );

        nullInfo.ToNullable().ShouldBeSameAs( nullInfo );
        var nonNullInfo2 = nullInfo.ToNonNullable();
        nonNullInfo2.ShouldBe( nonNullInfo );

        nonNullInfo.ToNonNullable().ShouldBeSameAs( nonNullInfo );
        var nullInfo2 = nonNullInfo.ToNullable();
        nullInfo2.ShouldBe( nullInfo );
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
        nullInfo.IsNullable.ShouldBeTrue();
        nullInfo.IsHomogeneous.ShouldBeTrue();
        nullInfo.ElementType.ShouldBeNull();
        nullInfo.GenericTypeArguments.Count.ShouldBe( 2 );
        nullInfo.Type.ShouldBe( typeof( Nullable<(int, string)> ) );

        var nonNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNonNullableValueTuple ) )! );
        nonNullInfo.IsNullable.ShouldBeFalse();
        nonNullInfo.IsHomogeneous.ShouldBeTrue();
        nonNullInfo.ElementType.ShouldBeNull();
        nonNullInfo.GenericTypeArguments.Count.ShouldBe( 2 );
        nonNullInfo.Type.ShouldBe( typeof( ValueTuple<int, string> ) );

        nullInfo.ToNullable().ShouldBeSameAs( nullInfo );
        var nonNullInfo2 = nullInfo.ToNonNullable();
        nonNullInfo2.ShouldBe( nonNullInfo );

        nonNullInfo.ToNonNullable().ShouldBeSameAs( nonNullInfo );
        var nullInfo2 = nonNullInfo.ToNullable();
        nullInfo2.ShouldBe( nullInfo );
    }

    [Test]
    public void field_nullable_value_tuple_nullability_information()
    {
        var f = new ExtMemberInfoFactory();
        var nullInfo = f.CreateNullabilityInfo( GetType().GetField( nameof( NullableField ) )! );
        nullInfo.IsNullable.ShouldBeTrue();
        nullInfo.IsHomogeneous.ShouldBeTrue();
        nullInfo.ElementType.ShouldBeNull();
        nullInfo.GenericTypeArguments.Count.ShouldBe( 2 );
        nullInfo.Type.ShouldBe( typeof( Nullable<(int, string)> ) );

        var nonNullInfo = f.CreateNullabilityInfo( GetType().GetField( nameof( NonNullableField ) )! );
        nonNullInfo.IsNullable.ShouldBeFalse();
        nonNullInfo.IsHomogeneous.ShouldBeTrue();
        nonNullInfo.ElementType.ShouldBeNull();
        nonNullInfo.GenericTypeArguments.Count.ShouldBe( 2 );
        nonNullInfo.Type.ShouldBe( typeof( ValueTuple<int, string> ) );

        nullInfo.ToNullable().ShouldBeSameAs( nullInfo );
        var nonNullInfo2 = nullInfo.ToNonNullable();
        nonNullInfo2.ShouldBe( nonNullInfo );

        nonNullInfo.ToNonNullable().ShouldBeSameAs( nonNullInfo );
        var nullInfo2 = nonNullInfo.ToNullable();
        nullInfo2.ShouldBe( nullInfo );
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
        rNullInfo.IsNullable.ShouldBe( true );
        rNullInfo.IsHomogeneous.ShouldBe( true );
        rNullInfo.ReflectsReadState.ShouldBe( true );
        rNullInfo.ReflectsWriteState.ShouldBe( true );

        var rNonNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNonNullableValueTupleHeterogeneous ) )!, useReadState: false );
        rNonNullInfoW.IsNullable.ShouldBe( false );
        rNonNullInfoW.IsHomogeneous.ShouldBe( true );
        rNonNullInfoW.ReflectsReadState.ShouldBe( true );
        rNonNullInfoW.ReflectsWriteState.ShouldBe( true );

        // Fields are like ref properties: homogeneity is by design.
        var fNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNullableValueTupleHeterogeneous ) )! );
        fNullInfo.IsNullable.ShouldBe( true );
        fNullInfo.IsHomogeneous.ShouldBe( true );
        fNullInfo.ReflectsReadState.ShouldBe( true );
        fNullInfo.ReflectsWriteState.ShouldBe( true );

        var fNonNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( RefGetNonNullableValueTupleHeterogeneous ) )!, useReadState: false );
        fNonNullInfoW.IsNullable.ShouldBe( false );
        fNonNullInfoW.IsHomogeneous.ShouldBe( true );
        fNonNullInfoW.ReflectsReadState.ShouldBe( true );
        fNonNullInfoW.ReflectsWriteState.ShouldBe( true );

        // Regular properties can be heterogeneous if and only if the type is nullable.
        var vNullInfo = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNullableValueTupleHeterogeneous ) )! );
        vNullInfo.IsNullable.ShouldBe( true );
        vNullInfo.IsHomogeneous.ShouldBe( false );
        vNullInfo.ReflectsReadState.ShouldBe( true );
        vNullInfo.ReflectsWriteState.ShouldBe( false );
        var vNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNullableValueTupleHeterogeneous ) )!, useReadState: false );
        vNullInfoW.IsNullable.ShouldBe( false );
        vNullInfoW.IsHomogeneous.ShouldBe( false );
        vNullInfoW.ReflectsReadState.ShouldBe( false );
        vNullInfoW.ReflectsWriteState.ShouldBe( true );

        // Error CS0037  Cannot convert null to '(int A, string? B)' because it is a non - nullable value type.
        // GetNonNullableValueTupleHeterogeneous = null;
        //
        // This is correctly handled: [AllowNull] is ignored.
        var vNonNullInfoW = f.CreateNullabilityInfo( GetType().GetProperty( nameof( GetNonNullableValueTupleHeterogeneous ) )!, useReadState: false );
        vNonNullInfoW.IsNullable.ShouldBe( false );
        vNonNullInfoW.IsHomogeneous.ShouldBe( true );
        vNonNullInfoW.ReflectsReadState.ShouldBe( true );
        vNonNullInfoW.ReflectsWriteState.ShouldBe( true );
    }

    public class NoConstraint<T> { }

    public class NotNullConstraint<T> where T : notnull { }

    public Type NoConstraintIntDirect = typeof( NoConstraint<int> );
    public NoConstraint<int> NoConstraintIntField = default!;

    public Type NoConstraintObjectDirect = typeof( NoConstraint<object> );
    public NoConstraint<object> NoConstraintObjectField = default!;

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.

    public Type NotNullConstraintObjectDirect = typeof( NotNullConstraint<object?> );
    public NotNullConstraint<object?> NotNullConstraintObjectField = default!;

#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.

    [Test]
    public void nullablility_information_for_types_is_nullable_oblivious()
    {
        var f = new ExtMemberInfoFactory();

        var ncif = f.CreateNullabilityInfo( GetType().GetField( nameof( NoConstraintIntField ) )! );
        ncif.IsNullable.ShouldBeFalse();
        ncif.GenericTypeArguments[0].IsNullable.ShouldBeFalse( "Value type: no issue." );

        var ncid = f.CreateNullabilityInfo( typeof( NoConstraint<int> ) );
        ncid.IsNullable.ShouldBeTrue();
        ncid.GenericTypeArguments[0].IsNullable.ShouldBeFalse( "Value type: no issue." );

        var ncof = f.CreateNullabilityInfo( GetType().GetField( nameof( NoConstraintObjectField ) )! );
        ncof.IsNullable.ShouldBeFalse();
        ncof.GenericTypeArguments[0].IsNullable.ShouldBeFalse( "Through member, nullability is detected." );

        var ncod = f.CreateNullabilityInfo( typeof( NoConstraint<object> ) );
        ncod.IsNullable.ShouldBeTrue();
        ncod.GenericTypeArguments[0].IsNullable.ShouldBeTrue( ":-(" );

        // NotNullConstraint<object?> with a notnull constraint.
        var nncof = f.CreateNullabilityInfo( GetType().GetField( nameof( NotNullConstraintObjectField ) )! );
        nncof.IsNullable.ShouldBeFalse();
        nncof.GenericTypeArguments[0].IsNullable.ShouldBeTrue( "The warning is ignored, the actual definition wins against the generic constraint. Good!" );

        var nncod = f.CreateNullabilityInfo( NotNullConstraintObjectDirect );
        nncod.IsNullable.ShouldBeTrue();
        nncod.GenericTypeArguments[0].IsNullable.ShouldBeTrue( "No surprise." );
    }

#pragma warning disable IDE0059 // Unnecessary assignment of a value
    // A boxed nullable value type don't exist unless awful tricks are applied.
    [Test]
    public void object_reference_cannot_hold_a_nullable_value_type()
    {
        object? o = null;
        int? v = 5;

        o = v;
        o.GetType().ShouldBe( typeof( int ) );
        v = null;

        o = v;
        o.ShouldBeNull();

    }
#pragma warning restore IDE0059 // Unnecessary assignment of a value
}
