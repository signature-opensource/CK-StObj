using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using NUnit.Framework;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.CrisLike;

[TestFixture]
public class CommandLikeTests
{
    public interface IBase : ICommand { }
    [CKTypeDefiner]
    public interface IRight : ICommand<int> { }
    [CKTypeDefiner]
    public interface IRight2 : IRight { }
    [CKTypeDefiner]
    public interface ILeft : ICommand<string> { }
    [CKTypeDefiner]
    public interface ILeft2 : ILeft, ICommand<object> { }

    public interface IUnified1 : IBase, IRight2 { }
    public interface IUnified2 : IBase, ILeft2 { }

    [Test]
    public void secondaries_are_available()
    {
        {
            var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IUnified1 )] );
            var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );
            var command = ts.FindByType<IPrimaryPocoType>( typeof( IBase ) );
            Throw.DebugAssert( command != null );
            var unified1 = ts.FindByType<ISecondaryPocoType>( typeof( IUnified1 ) );
            Throw.DebugAssert( unified1 != null );
            command.SecondaryTypes.ShouldHaveSingleItem().ShouldBe( unified1 );
        }
        {
            var r = TestHelper.GetSuccessfulCollectorResult( [typeof( IUnified2 ), typeof( IUnified1 )] );
            var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );
            var command = ts.FindByType<IPrimaryPocoType>( typeof( IBase ) );
            Throw.DebugAssert( command != null );
            var unified1 = ts.FindByType<ISecondaryPocoType>( typeof( IUnified1 ) );
            var unified2 = ts.FindByType<ISecondaryPocoType>( typeof( IUnified2 ) );
            Throw.DebugAssert( unified1 != null && unified2 != null );
            command.SecondaryTypes.ShouldBe( [unified1, unified2], ignoreOrder: true );
        }
    }

    [Test]
    public void incompatible_command_result_detection()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( ILeft ), typeof( IRight ), typeof( IUnified1 ), typeof( IUnified2 )] );
        var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );

        var command = ts.FindByType<IPrimaryPocoType>( typeof( IBase ) );
        Throw.DebugAssert( command != null && command.IsNullable );
        command = command.NonNullable;

        var icr = ts.FindGenericTypeDefinition( typeof( ICommand<> ) );
        Throw.DebugAssert( icr != null );
        var withResult = command.AbstractTypes.Where( a => a.GenericTypeDefinition == icr );
        withResult.Count().ShouldBe( 3 );

        var minimal = withResult.ComputeMinimal();
        minimal.Count.ShouldBe( 2 );
        minimal.Select( m => m.ToString() ).OrderBy( Util.FuncIdentity ).ShouldBe(
        [
            "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<int>",
            "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<string>"
        ] );
    }


    public interface IResult : IPoco
    {
        int Val { get; set; }
    }

    /// <summary>
    /// Extends the basic result with a <see cref="MoreVal"/>.
    /// </summary>
    public interface IMoreResult : IResult
    {
        /// <summary>
        /// Gets or sets the More value.
        /// </summary>
        int MoreVal { get; set; }
    }

    public interface IAnotherResult : IResult
    {
        int AnotherVal { get; set; }
    }

    public interface IUnifiedResult : IMoreResult, IAnotherResult { }

    public interface ICommandWithPocoResult : ICommand<IResult> { }

    public interface ICommandWithMorePocoResult : ICommandWithPocoResult, ICommand<IMoreResult> { }

    public interface ICommandWithAnotherPocoResult : ICommandWithPocoResult, ICommand<IAnotherResult> { }

    public interface ICommandUnifiedWithTheResult : ICommandWithMorePocoResult, ICommandWithAnotherPocoResult, ICommand<IUnifiedResult> { }

    [Test]
    public void multiple_command_result_resolution()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( ICommandUnifiedWithTheResult ), typeof( IUnifiedResult )] );
        var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );

        IPocoGenericTypeDefinition? commandWithResultType = ts.FindGenericTypeDefinition( typeof( ICommand<> ) );
        Throw.DebugAssert( commandWithResultType != null );

        var command = ts.FindByType<IPrimaryPocoType>( typeof( ICommandWithPocoResult ) );
        Throw.DebugAssert( command != null );
        command.AllAbstractTypes.Count.ShouldBe( 6 );
        command.AbstractTypes.ShouldBe( command.AllAbstractTypes, "No ImplementationLess since we registered the IUnifiedResult." );
        // Consider all ICommand<T> and reduce them.
        var withResult = command.AbstractTypes.Where( a => a.GenericTypeDefinition == commandWithResultType );
        var reduced = withResult.ComputeMinimal();
        reduced.Count.ShouldBe( 1 );
        reduced[0].GenericArguments[0].Type.ToString().ShouldBe( "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IUnifiedResult?" );
    }

    [Test]
    public void TypeSet_from_secondary()
    {
        var r = TestHelper.GetSuccessfulCollectorResult( [typeof( ICommandUnifiedWithTheResult ), typeof( IUnifiedResult )] );
        var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );

        var withCommandButNotItsResult = ts.SetManager.EmptyExchangeable.Include( [ts.FindByType( typeof( ICommandUnifiedWithTheResult ) )!] );
        withCommandButNotItsResult.NonNullableTypes.Select(t => t.ToString()).ShouldBe( 
            [
                "[PrimaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.ICommandWithPocoResult",
                "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.ICommandWithMorePocoResult",
                "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.ICommandWithAnotherPocoResult",
                "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.ICommandUnifiedWithTheResult",
                "[PrimaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IResult",
                "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IMoreResult",
                "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IAnotherResult",
                "[SecondaryPoco]CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IUnifiedResult",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IUnifiedResult>",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IMoreResult>",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IAnotherResult>",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<CK.StObj.Engine.Tests.CrisLike.CommandLikeTests.IResult>",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.IAbstractCommand",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICrisPoco",
                "[AbstractPoco]CK.Core.IPoco",
                "[Basic]int"
            ], ignoreOrder: true );

    }

}
