using Shouldly;
using NUnit.Framework;
using System.Reflection;

namespace CK.StObj.Engine.Tests.Poco;

[TestFixture]
public class GenericTypeParameterNullabilityDetectionTests
{
    public interface ICommandNullable<out TResult>
    {
        static ICommandNullable<TResult> TResultTypeViaHolder => default!;
        static TResult TResultType => default!;
    }

    public interface ICommandNotNull<out TResult> where TResult : notnull
    {
        static ICommandNullable<TResult> TResultTypeViaHolder => default!;
        static TResult TResultType => default!;
    }

    [Test]
    public void generic_parameter_nullablity_detection_trick_fails_to_detect_root_nullability()
    {
        // ICommandNullable has no notnull constraint. 

        // For value type it works.
        var nCtx = new TEMPNullabilityInfoContext();
        {
            var p = typeof( ICommandNullable<int?> ).GetProperty( "TResultType", BindingFlags.Static | BindingFlags.Public )!;
            var nP = nCtx.Create( p );
            nP.ReadState.ShouldBe( NullabilityState.Nullable );
            nP.WriteState.ShouldBe( NullabilityState.Unknown, "Useless" );
        }
        {
            var p = typeof( ICommandNullable<int> ).GetProperty( "TResultType", BindingFlags.Static | BindingFlags.Public )!;
            var nP = nCtx.Create( p );
            nP.ReadState.ShouldBe( NullabilityState.NotNull );
            nP.WriteState.ShouldBe( NullabilityState.Unknown, "Useless." );
        }
        {
            var p = typeof( ICommandNullable<int?> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.Nullable );
            nP.WriteState.ShouldBe( NullabilityState.Nullable );
        }
        {
            var p = typeof( ICommandNullable<int> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.NotNull );
            nP.WriteState.ShouldBe( NullabilityState.NotNull, "Failing." );
        }
        // But this fails for reference types: the result type is always nullable.
        {
            var p = typeof( ICommandNullable<object?> ).GetProperty( "TResultType", BindingFlags.Static | BindingFlags.Public )!;
            var nP = nCtx.Create( p );
            nP.ReadState.ShouldBe( NullabilityState.Nullable );
            nP.WriteState.ShouldBe( NullabilityState.Unknown, "Useless" );
        }
        {
            var p = typeof( ICommandNullable<object> ).GetProperty( "TResultType", BindingFlags.Static | BindingFlags.Public )!;
            var nP = nCtx.Create( p );
            nP.ReadState.ShouldBe( NullabilityState.Nullable, "Failing." );
            nP.WriteState.ShouldBe( NullabilityState.Unknown, "Useless." );
        }
        {
            var p = typeof( ICommandNullable<object?> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.Nullable );
            nP.WriteState.ShouldBe( NullabilityState.Nullable );
        }
        {
            var p = typeof( ICommandNullable<object> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.Nullable, "Failing." );
            nP.WriteState.ShouldBe( NullabilityState.Nullable, "Failing." );
        }
        // Explicit use of the ICommandNotNull with the notnull constraint works.
        {
            var p = typeof( ICommandNotNull<object> ).GetProperty( "TResultType", BindingFlags.Static | BindingFlags.Public )!;
            var nP = nCtx.Create( p );
            nP.ReadState.ShouldBe( NullabilityState.NotNull );
            nP.WriteState.ShouldBe( NullabilityState.Unknown, "Useless" );
        }
        {
            var p = typeof( ICommandNotNull<object> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.NotNull );
            nP.WriteState.ShouldBe( NullabilityState.NotNull );
        }
        // But if you forget the warning, this fails: we cannot detect your intention and the result
        // will be considered not nullable.
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        {
            var p = typeof( ICommandNotNull<object?> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.NotNull );
            nP.WriteState.ShouldBe( NullabilityState.NotNull );
        }
        // ... but it works for value type ...
        {
            var p = typeof( ICommandNotNull<int?> ).GetProperty( "TResultTypeViaHolder", BindingFlags.Static | BindingFlags.Public )!;
            var nInfo = nCtx.Create( p );
            var nP = nInfo.GenericTypeArguments[0];
            nP.ReadState.ShouldBe( NullabilityState.Nullable );
            nP.WriteState.ShouldBe( NullabilityState.Nullable );
        }
#pragma warning restore CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        // The "reflection property trick" is not useless since we need a Property/Field or EventInfo to obtain
        // a nullability information and ICommand has no property to offer, but root nullability cannot be determined.
        // There seems to be no way to workaround this limitation.

    }
}
