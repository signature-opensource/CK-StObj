using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.CrisLike
{
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
                var c = TestHelper.CreateStObjCollector( typeof( IUnified1 ) );
                var r = TestHelper.GetSuccessfulResult( c );
                var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );
                var command = ts.FindByType<IPrimaryPocoType>( typeof( IBase ) );
                Throw.DebugAssert( command != null );
                var unified1 = ts.FindByType<ISecondaryPocoType>( typeof( IUnified1 ) );
                Throw.DebugAssert( unified1 != null );
                command.SecondaryTypes.Should().HaveCount( 1 ).And.Contain( unified1 );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IUnified2 ), typeof( IUnified1 ) );
                var r = TestHelper.GetSuccessfulResult( c );
                var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );
                var command = ts.FindByType<IPrimaryPocoType>( typeof( IBase ) );
                Throw.DebugAssert( command != null );
                var unified1 = ts.FindByType<ISecondaryPocoType>( typeof( IUnified1 ) );
                var unified2 = ts.FindByType<ISecondaryPocoType>( typeof( IUnified2 ) );
                Throw.DebugAssert( unified1 != null && unified2 != null );
                command.SecondaryTypes.Should().HaveCount( 2 ).And.Contain( new[] { unified1, unified2 } );
            }
        }

        [Test]
        public void incompatible_command_result_detection()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ILeft ), typeof( IRight ), typeof( IUnified1 ), typeof( IUnified2 ) );
            var r = TestHelper.GetSuccessfulResult( c );
            var ts = r.PocoTypeSystemBuilder.Lock( TestHelper.Monitor );

            var command = ts.FindByType<IPrimaryPocoType>( typeof( IBase ) );
            Throw.DebugAssert( command != null && command.IsNullable );
            command = command.NonNullable;

            var icr = ts.FindGenericTypeDefinition( typeof( ICommand<> ) );
            Throw.DebugAssert( icr != null );
            var withResult = command.AbstractTypes.Where( a => a.GenericTypeDefinition == icr );
            withResult.Should().HaveCount( 3 );

            var minimal = withResult.ComputeMinimal();
            minimal.Should().HaveCount( 2 );
            minimal.Select( m => m.ToString() ).OrderBy( Util.FuncIdentity ).Should().BeEquivalentTo( new[]
            {
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<int>",
                "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<string>"
            } );
        }
    }
}
