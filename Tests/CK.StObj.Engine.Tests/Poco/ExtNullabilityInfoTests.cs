using CK.Setup;
using FluentAssertions;
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
        ncif.IsNullable.Should().BeFalse();
        ncif.GenericTypeArguments[0].IsNullable.Should().BeFalse( "Value type: no issue." );

        var ncid = f.CreateNullabilityInfo( typeof( NoConstraint<int> ) );
        ncid.IsNullable.Should().BeTrue();
        ncid.GenericTypeArguments[0].IsNullable.Should().BeFalse( "Value type: no issue." );

        var ncof = f.CreateNullabilityInfo( GetType().GetField( nameof( NoConstraintObjectField ) )! );
        ncof.IsNullable.Should().BeFalse();
        ncof.GenericTypeArguments[0].IsNullable.Should().BeFalse( "Through member, nullability is detected." );

        var ncod = f.CreateNullabilityInfo( typeof( NoConstraint<object> ) );
        ncod.IsNullable.Should().BeTrue();
        ncod.GenericTypeArguments[0].IsNullable.Should().BeTrue( ":-(" );

        // NotNullConstraint<object?> with a notnull constraint.
        var nncof = f.CreateNullabilityInfo( GetType().GetField( nameof( NotNullConstraintObjectField ) )! );
        nncof.IsNullable.Should().BeFalse();
        nncof.GenericTypeArguments[0].IsNullable.Should().BeTrue( "The warning is ignored, the actual definition wins against the generic constraint. Good!" );

        var nncod = f.CreateNullabilityInfo( NotNullConstraintObjectDirect );
        nncod.IsNullable.Should().BeTrue();
        nncod.GenericTypeArguments[0].IsNullable.Should().BeTrue( "No surprise." );
    }

#pragma warning disable IDE0059 // Unnecessary assignment of a value
    // A boxed nullable value type don't exist unless awful tricks are applied.
    [Test]
    public void object_reference_cannot_hold_a_nullable_value_type()
    {
        object? o = null;
        int? v = 5;

        o = v;
        o.GetType().Should().Be( typeof(int) );
        v = null;

        o = v;
        o.Should().BeNull();

    }
#pragma warning restore IDE0059 // Unnecessary assignment of a value
}
