using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoGenericTests
    {
        public interface IAmAmbiguous<T> : IPoco
        {
            T Value { get; set; }
        }

        public interface IWantAnInt : IAmAmbiguous<int>
        {
        }

        public interface IWantAnObject : IAmAmbiguous<object>
        {
        }

        [Test]
        public void open_generic_IPoco_is_forbidden()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWantAnInt ) );
            TestHelper.GetFailedResult( c, "Use the [CKTypeDefiner] attribute to define a generic IPoco." );
        }

        [CKTypeDefiner]
        public interface IBuggy<T1, T2> : IPoco { }

        public interface IWillFail : IBuggy<string, int> { }

        [Test]
        public void generic_AbstractTypes_must_expose_the_parameter_type_property()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IWillFail ) );
            TestHelper.GetFailedResult( c,
                "Generic interface 'CK.StObj.Engine.Tests.Poco.PocoGenericTests.IBuggy<T1,T2>' must define '[AutoImplementationClaim] public static T1 T1Type => default!;' property. This is required for type analysis.",
                "Generic interface 'CK.StObj.Engine.Tests.Poco.PocoGenericTests.IBuggy<T1,T2>' must define '[AutoImplementationClaim] public static T2 T2Type => default!;' property. This is required for type analysis." );
        }

        [CKTypeSuperDefiner]
        public interface ICrisPoco : IPoco { }
        [CKTypeSuperDefiner]
        public interface IAbstractCommand : ICrisPoco { }

        public interface ICommand<out TResult> : IAbstractCommand
        {
            [AutoImplementationClaim]
            static public TResult TResultType => default!;
        }

        public interface ITopCommand : ICommand<object> { }

        [Test]
        public void generic_AbstractType_parameter_and_argument()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ITopCommand ) );
            // We generate the code and compile it to check any error.
            var r = TestHelper.GenerateCode( c, null, generateSourceFile: true, CompileOption.Compile );
            r.EngineResult.Success.Should().BeTrue();
            var ts = r.CollectorResult.CKTypeResult.PocoTypeSystem;
            var tDef = ts.FindGenericTypeDefinition( typeof( ICommand<> ) );
            Throw.DebugAssert( tDef != null );
            tDef.Type.Should().BeSameAs( typeof( ICommand<> ) );
            tDef.Parameters.Should().HaveCount( 1 );
            IPocoGenericParameter parameter = tDef.Parameters[0];
            parameter.Name.Should().Be( "TResult" );
            parameter.Attributes.Should().Be( GenericParameterAttributes.Covariant );

            tDef.Instances.Should().HaveCount( 1 );
            var cmd = tDef.Instances.Single();
            cmd.IsGenericType.Should().BeTrue();
            cmd.GenericTypeDefinition.Should().BeSameAs( tDef );
            cmd.GenericArguments.Should().HaveCount( 1 );
            var tResult = cmd.GenericArguments[0];
            tResult.Parameter.Should().BeSameAs( parameter );
            tResult.Type.Kind.Should().Be( PocoTypeKind.Any );
            tResult.Type.IsNullable.Should().BeTrue( "Unfortunately..." );
        }

        public interface IIntCommand : ITopCommand, ICommand<int> { }

        [Test]
        public void MinimalAbstractTypes_considers_generic_parameter_covariance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IIntCommand ) );
            // We generate the code and compile it to check any error.
            var r = TestHelper.GenerateCode( c, null, generateSourceFile: true, CompileOption.Compile );
            r.EngineResult.Success.Should().BeTrue();
            var ts = r.CollectorResult.CKTypeResult.PocoTypeSystem;
            var cmd = ts.FindByType<ISecondaryPocoType>( typeof( IIntCommand ) );
            Throw.DebugAssert( cmd != null );
            cmd.PrimaryPocoType.AbstractTypes.Should().HaveCount( 4 );
            cmd.PrimaryPocoType.AbstractTypes.Select( t => t.ToString() )
                .Should().BeEquivalentTo( new[] {
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.ICommand<object>",
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.IAbstractCommand",
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.ICrisPoco",
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.ICommand<int>" } );

            cmd.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Should().Be( "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.ICommand<int>" );
        }

        public interface IS1Command : ITopCommand, ICommand<IPoco> { }
        public interface IS2Command : IS1Command, ICommand<ICrisPoco> { }
        public interface IS3Command : IS2Command, ICommand<IAbstractCommand> { }
        public interface IS4Command : IS3Command, ICommand<ICommand<object>> { }
        public interface IS5Command : IS4Command, ICommand<ICommand<ICommand<object>>> { }
        // ICommand<int> is an Orphan AbstractPoco: it has no implementation.
        public interface IS6Command : IS5Command, ICommand<ICommand<ICommand<int>>> { }

        [Test]
        public void MinimalAbstractTypes_considers_recurse_generic_parameter_covariance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IS6Command ) );
            // We generate the code and compile it to check any error.
            var r = TestHelper.GenerateCode( c, null, generateSourceFile: true, CompileOption.Compile );
            r.EngineResult.Success.Should().BeTrue();
            var ts = r.CollectorResult.CKTypeResult.PocoTypeSystem;
            var cmd = ts.FindByType<ISecondaryPocoType>( typeof( IS6Command ) );
            Throw.DebugAssert( cmd != null );
            cmd.PrimaryPocoType.AbstractTypes.Should().HaveCount( 9 );

            cmd.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>",
                    "[AbstractPoco]IAbstractCommand",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>",
                    "[AbstractPoco]ICommand<ICrisPoco>",
                    "[AbstractPoco]ICommand<IAbstractCommand>",
                    "[AbstractPoco]ICommand<ICommand<object>>",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>",
                    "[AbstractPoco]ICommand<ICommand<ICommand<int>>>"
                } );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                .Should().Be( "[AbstractPoco]ICommand<ICommand<ICommand<int>>>" );
        }

        [CKTypeSuperDefiner]
        public interface IAbstractInput : ICrisPoco { }

        public interface IInput<in TInput> : IAbstractInput
        {
            [AutoImplementationClaim]
            static public TInput TInputType => default!;
        }

        public interface IIntInput : IInput<int> { }
        public interface IObjectInput : IIntInput, IInput<object> { }

        [Test]
        public void MinimalAbstractTypes_considers_generic_parameter_contravariance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IObjectInput ) );
            // We generate the code and compile it to check any error.
            var r = TestHelper.GenerateCode( c, null, generateSourceFile: true, CompileOption.Compile );
            r.EngineResult.Success.Should().BeTrue();
            var ts = r.CollectorResult.CKTypeResult.PocoTypeSystem;
            var cmd = ts.FindByType<ISecondaryPocoType>( typeof( IObjectInput ) );
            Throw.DebugAssert( cmd != null );
            cmd.PrimaryPocoType.AbstractTypes.Should().HaveCount( 4 );
            cmd.PrimaryPocoType.AbstractTypes.Select( t => t.ToString() )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.IInput<object>",
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.IAbstractInput",
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.ICrisPoco",
                    "[AbstractPoco]CK.StObj.Engine.Tests.Poco.PocoGenericTests.IInput<int>"
                } );

            cmd.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                .Should().Be( "[AbstractPoco]IInput<object>" );
        }

        // IInput<int> is an Orphan AbstractPoco: it has no implementation.
        public interface IBaseInput : IInput<IInput<IInput<int>>> { }
        public interface IExt1Input : IBaseInput, IInput<IInput<IInput<object>>> { }
        public interface IExt2Input : IExt1Input, IInput<IInput<object>> { }
        public interface IExt3Input : IExt2Input, IInput<IAbstractInput> { }
        public interface IExt4Input : IExt3Input, IInput<ICrisPoco> { }
        public interface IExt5Input : IExt4Input, IInput<IPoco> { }
        public interface IExt6Input : IExt5Input, IInput<object> { }

        [Test]
        public void MinimalAbstractTypes_considers_recurse_generic_parameter_contravariance()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IExt6Input ) );
            // We generate the code and compile it to check any error.
            var r = TestHelper.GenerateCode( c, null, generateSourceFile: true, CompileOption.Compile );
            r.EngineResult.Success.Should().BeTrue();
            var ts = r.CollectorResult.CKTypeResult.PocoTypeSystem;
            var cmd = ts.FindByType<IPrimaryPocoType>( typeof( IBaseInput ) );
            Throw.DebugAssert( cmd != null );
            cmd.AbstractTypes.Should().HaveCount( 9 );

            cmd.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]IInput<object>",
                    "[AbstractPoco]IAbstractInput",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]IInput<CK.Core.IPoco>",
                    "[AbstractPoco]IInput<ICrisPoco>",
                    "[AbstractPoco]IInput<IAbstractInput>",
                    "[AbstractPoco]IInput<IInput<object>>",
                    "[AbstractPoco]IInput<IInput<IInput<object>>>",
                    "[AbstractPoco]IInput<IInput<IInput<int>>>"
                } );
            cmd.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                .Should().Be( "[AbstractPoco]IInput<object>" );
        }

    }
}
