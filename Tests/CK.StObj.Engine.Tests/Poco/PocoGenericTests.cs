using CK.Core;
using CK.Setup;
using CK.StObj.Engine.Tests.CrisLike;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
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
            var c = TestHelper.CreateTypeCollector( typeof( IWantAnInt ) );
            TestHelper.GetFailedCollectorResult( c, "Use the [CKTypeDefiner] attribute to define a generic IPoco." );
        }

        [CKTypeDefiner]
        public interface IBuggy<T1, T2> : IPoco { }

        public interface IWillFail : IBuggy<string, int> { }

        [Test]
        public void generic_AbstractTypes_must_expose_the_parameter_type_property()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IWillFail ) );
            TestHelper.GetFailedCollectorResult( c,
                "Generic interface 'CK.StObj.Engine.Tests.Poco.PocoGenericTests.IBuggy<T1,T2>' must define '[AutoImplementationClaim] public static T1 T1Type => default!;' property. This is required for type analysis.",
                "Generic interface 'CK.StObj.Engine.Tests.Poco.PocoGenericTests.IBuggy<T1,T2>' must define '[AutoImplementationClaim] public static T2 T2Type => default!;' property. This is required for type analysis." );
        }

        public interface ITopCommand : ICommand<object> { }

        [Test]
        public void generic_AbstractType_parameter_and_argument()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( ITopCommand ) );
            var engineResult = configuration.RunSuccessfully();

            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;
            Throw.DebugAssert( ts != null );

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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( IIntCommand ));
            var engineResult = configuration.RunSuccessfully();

            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

            var cmdNullable = ts.FindByType<ISecondaryPocoType>( typeof( IIntCommand ) );
            Throw.DebugAssert( cmdNullable != null );
            Throw.DebugAssert( cmdNullable.IsNullable );
            cmdNullable.PrimaryPocoType.AbstractTypes.Should().HaveCount( 4 );
            cmdNullable.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[] {
                    "[AbstractPoco]ICommand<object>?",
                    "[AbstractPoco]IAbstractCommand?",
                    "[AbstractPoco]ICrisPoco?",
                    "[AbstractPoco]ICommand<int>?" } );

            cmdNullable.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmdNullable.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Should().Be( "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<int>?" );

            var cmd = cmdNullable.NonNullable;
            cmd.PrimaryPocoType.AbstractTypes.Should().HaveCount( 4 );
            cmd.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace("CK.StObj.Engine.Tests.CrisLike.", "") )
                .Should().BeEquivalentTo( new[] {
                    "[AbstractPoco]ICommand<object>",
                    "[AbstractPoco]IAbstractCommand",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]ICommand<int>" } );

            cmd.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Should().Be( "[AbstractPoco]CK.StObj.Engine.Tests.CrisLike.ICommand<int>" );
        }

        // Given ITopCommand : ICommand<object>, a command that returns an object.
        // 
        // At the Type System level, we cannot tell that IC : IComman<int>, ICommand<string> is invalid.
        // Cris checks that all the ICommand<TResult> of a command can be resolved to the most precise
        // existing type. This uses the MinimalAbstractTypes that resolves this with co (out) and contra (in)
        // generic parameter constraints (we don't use in constraint but it is implemented).
        IPocoType GetCommandResult( IPrimaryPocoType cmd )
        {
            // The Single must not throw!
            var unique = cmd.MinimalAbstractTypes.Single( a => a.IsGenericType && a.GenericTypeDefinition.Type == typeof( ICommand<> ) );
            return unique.GenericArguments[0].Type;
        }

        // IS1Command can be a secondary interface because it exists a IPoco, the ITopCommand itself.
        // At this stage (without any other concrete Poco in the type system), ITopCommand is condemned to return another ITopCommand.
        public interface IS1Command : ITopCommand, ICommand<IPoco> { }
        // IS2 and IS3 bring a similar (more restricted) constraint.
        public interface IS2Command : IS1Command, ICommand<ICrisPoco> { }
        public interface IS3Command : IS2Command, ICommand<IAbstractCommand> { }
        // IS4 states that ITopCommand must return a command that returns an object. Everything is fine: ITopCommand is a command
        // that returns an object.
        public interface IS4Command : IS3Command, ICommand<ICommand<object>> { }
        // IS5Command states that ITopCommand must return a command that returns a command that returns an object: this is the case
        // of ITopCommand. Everything is fine.
        public interface IS5Command : IS4Command, ICommand<ICommand<ICommand<object>>> { }

        [Test]
        public void commands_with_multiple_returns()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(typeof( IS5Command ));
            var engineResult = configuration.RunSuccessfully();

            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

            var cmdNullable = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
            Throw.DebugAssert( cmdNullable != null );
            cmdNullable.IsNullable.Should().BeTrue();
            cmdNullable.AllAbstractTypes.Should().BeEquivalentTo( cmdNullable.AbstractTypes );
            cmdNullable.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                               .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>?",
                    "[AbstractPoco]IAbstractCommand?",
                    "[AbstractPoco]ICrisPoco?",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>?",
                    "[AbstractPoco]ICommand<ICrisPoco>?",
                    "[AbstractPoco]ICommand<IAbstractCommand>?",
                    "[AbstractPoco]ICommand<ICommand<object>>?",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>?"
                } );
            GetCommandResult( cmdNullable ).ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                      .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]ICommand<ICommand<object>>?" );

            var cmd = cmdNullable.NonNullable;
            cmd.AllAbstractTypes.Should().BeEquivalentTo( cmd.AbstractTypes );
            cmd.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                       .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>",
                    "[AbstractPoco]IAbstractCommand",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>",
                    "[AbstractPoco]ICommand<ICrisPoco>",
                    "[AbstractPoco]ICommand<IAbstractCommand>",
                    "[AbstractPoco]ICommand<ICommand<object>>",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>"
                } );
            GetCommandResult( cmd ).ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                              .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]ICommand<ICommand<object>>?" );
        }

        // If we introduce a IS6NoWayCommand that states that ITopCommand should actually return an int, this fails because of
        // IS1Command (and the others).
        // There's no precedence rule of any kind that would allow a choice: the system is invalid.
        public interface IS6NoWayCommand1 : IS1Command, ICommand<int> { }
        public interface IS6NoWayCommand2 : IS5Command, ICommand<int> { }

        [Test]
        public void conflicting_commands_with_multiple_returns()
        {
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.AddTypes( typeof( IS6NoWayCommand1 ) );
                var engineResult = configuration.RunSuccessfully();

                var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

                var cmd = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
                Throw.DebugAssert( cmd != null );
                FluentActions.Invoking( () => GetCommandResult( cmd ) ).Should().Throw<Exception>();
            }
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.AddTypes( typeof( IS6NoWayCommand2 ) );
                var engineResult = configuration.RunSuccessfully();

                var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

                var cmd = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
                Throw.DebugAssert( cmd != null );
                FluentActions.Invoking( () => GetCommandResult( cmd ) ).Should().Throw<Exception>();
            }
        }

        // Same if we want IS6 to return a ICommand<int>: it conflicts with IS5Command.
        public interface IS6ExcludeIS5Command : ITopCommand, ICommand<ICommand<int>> { }

        // Now, what if we introduce IS6Command that states that ITopCommand must return a ICommand<ICommand<int>>. This
        // doesn't conflict with IS5. But there is an issue: there is no command in the type system that returns an int...
        // One option would be to consider the whole system invalid. This would prevent "partial" systems to exist and that
        // doesn't seem to be a great idea: partial/incomplete systems as long as they don't expose contradictions are fine.
        // A better solution is to kindly "ignore" this unsolvable constraint: the ICommand<ICommand<ICommand<int>>> abstract
        // interface is marked ImplementationLess but the IS6 secondary poco exists and is taken into account.
        public interface IS6Command : IS5Command, ICommand<ICommand<ICommand<int>>> { }

        // If at least one command that returns an int exists in the Type System, then the command resolution fails.
        public interface ICommandWithInt : ICommand<int> { }

        [Test]
        public void ImplementationLess_allows_partial_type_system()
        {
            // With only the IS6ExcludeIS5Command, the ITopCommand : ICommand<object> returns an object.
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.AddTypes(  typeof( IS6ExcludeIS5Command ) );
                var engineResult = configuration.RunSuccessfully();
                var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

                var cmd = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
                Throw.DebugAssert( cmd != null );
                GetCommandResult( cmd ).ToString()
                    .Should().Be( "[Any]object?" );
            }
            // With IS6ExcludeIS5Command and IS5Command bu no command that return a int, IS5Command is fine.
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.AddTypes(  typeof( IS6ExcludeIS5Command ), typeof( IS5Command ) );
                var engineResult = configuration.RunSuccessfully();
                var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

                var cmd = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
                Throw.DebugAssert( cmd != null );
                GetCommandResult( cmd ).ToString().Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                    .Should().Be( "[AbstractPoco]ICommand<ICommand<object>>?" );
            }
            // With IS6ExcludeIS5Command, IS5Command and a command that return a int, the return cannot be resolved.
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.AddTypes(  typeof( IS6ExcludeIS5Command ), typeof( IS5Command ), typeof( ICommandWithInt ) );
                var engineResult = configuration.RunSuccessfully();
                var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

                var cmd = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
                Throw.DebugAssert( cmd != null );
                FluentActions.Invoking( () => GetCommandResult( cmd ) ).Should().Throw<Exception>();
            }
        }

        [Test]
        public void MinimalAbstractTypes_considers_recurse_generic_parameter_covariance()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes(  typeof( IS6Command ) );
            var engineResult = configuration.RunSuccessfully();
            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;
            Throw.DebugAssert( ts != null );

            var cmdNullable = ts.FindByType<ISecondaryPocoType>( typeof( IS6Command ) );
            Throw.DebugAssert( cmdNullable != null );
            Throw.DebugAssert( cmdNullable.IsNullable );

            // AllAbstractTypes includes ImplementationLess abstract poco.
            cmdNullable.PrimaryPocoType.AllAbstractTypes.Should().HaveCount( 9 );
            cmdNullable.PrimaryPocoType.AllAbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                                  .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>?",
                    "[AbstractPoco]IAbstractCommand?",
                    "[AbstractPoco]ICrisPoco?",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>?",
                    "[AbstractPoco]ICommand<ICrisPoco>?",
                    "[AbstractPoco]ICommand<IAbstractCommand>?",
                    "[AbstractPoco]ICommand<ICommand<object>>?",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>?",
                    "[AbstractPoco]ICommand<ICommand<ICommand<int>>>?"
                } );

            // AbstractTypes DOES NOT include ImplementationLess abstract poco.
            cmdNullable.PrimaryPocoType.AllAbstractTypes.Should().StartWith( cmdNullable.PrimaryPocoType.AbstractTypes, "This is how this is currenlty implemented." );

            cmdNullable.PrimaryPocoType.AbstractTypes.Should().HaveCount( 8 );
            cmdNullable.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                               .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>?",
                    "[AbstractPoco]IAbstractCommand?",
                    "[AbstractPoco]ICrisPoco?",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>?",
                    "[AbstractPoco]ICommand<ICrisPoco>?",
                    "[AbstractPoco]ICommand<IAbstractCommand>?",
                    "[AbstractPoco]ICommand<ICommand<object>>?",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>?"
                } );

            cmdNullable.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmdNullable.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                                .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]ICommand<ICommand<ICommand<object>>>?" );

            var cmd = cmdNullable.NonNullable;
            // AllAbstractTypes includes ImplementationLess abstract poco.
            cmd.PrimaryPocoType.AllAbstractTypes.Should().HaveCount( 9 );
            cmd.PrimaryPocoType.AllAbstractTypes.Select( t => t.ToString().Replace("CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "").Replace("CK.StObj.Engine.Tests.CrisLike.", "")
            )
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

            // AbstractTypes DOES NOT include ImplementationLess abstract poco.
            cmd.PrimaryPocoType.AllAbstractTypes.Should().StartWith( cmd.PrimaryPocoType.AbstractTypes, "This is how this is currenlty implemented." );

            cmd.PrimaryPocoType.AbstractTypes.Should().HaveCount( 8 );
            cmd.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                       .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>",
                    "[AbstractPoco]IAbstractCommand",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>",
                    "[AbstractPoco]ICommand<ICrisPoco>",
                    "[AbstractPoco]ICommand<IAbstractCommand>",
                    "[AbstractPoco]ICommand<ICommand<object>>",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>"
                } );

            cmd.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                        .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]ICommand<ICommand<ICommand<object>>>" );


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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( IObjectInput ) );
            var engineResult = configuration.RunSuccessfully();
            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

            var cmdNullable = ts.FindByType<ISecondaryPocoType>( typeof( IObjectInput ) );
            Throw.DebugAssert( cmdNullable != null );
            Throw.DebugAssert( cmdNullable.IsNullable );
            cmdNullable.PrimaryPocoType.AbstractTypes.Should().HaveCount( 4 );
            cmdNullable.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                               .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]IInput<object>?",
                    "[AbstractPoco]IAbstractInput?",
                    "[AbstractPoco]ICrisPoco?",
                    "[AbstractPoco]IInput<int>?"
                } );

            cmdNullable.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmdNullable.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                                .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]IInput<object>?" );

            var cmd = cmdNullable.NonNullable;
            cmd.PrimaryPocoType.AbstractTypes.Should().HaveCount( 4 );
            cmd.PrimaryPocoType.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                       .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]IInput<object>",
                    "[AbstractPoco]IAbstractInput",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]IInput<int>"
                } );

            cmd.PrimaryPocoType.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.PrimaryPocoType.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                        .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]IInput<object>" );

        }

        // IInput<int> is an ImplementationLess AbstractPoco: it has no implementation.
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
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( IExt6Input ) );
            var engineResult = configuration.RunSuccessfully();
            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;
            
            var cmdNullable = ts.FindByType<IPrimaryPocoType>( typeof( IBaseInput ) );
            Throw.DebugAssert( cmdNullable != null );
            Throw.DebugAssert( cmdNullable.IsNullable );
            cmdNullable.AllAbstractTypes.Should().HaveCount( 9 );
            // IInput<int> is ImplementationLess.
            cmdNullable.AbstractTypes.Should().HaveCount( 8 );

            cmdNullable.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                               .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]IInput<object>?",
                    "[AbstractPoco]IAbstractInput?",
                    "[AbstractPoco]ICrisPoco?",
                    "[AbstractPoco]IInput<CK.Core.IPoco>?",
                    "[AbstractPoco]IInput<ICrisPoco>?",
                    "[AbstractPoco]IInput<IAbstractInput>?",
                    "[AbstractPoco]IInput<IInput<object>>?",
                    "[AbstractPoco]IInput<IInput<IInput<object>>>?",
                } );
            cmdNullable.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmdNullable.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]IInput<object>?" );

            var cmd = cmdNullable.NonNullable;
            cmd.AllAbstractTypes.Should().HaveCount( 9 );
            // IInput<int> is ImplementationLess.
            cmd.AbstractTypes.Should().HaveCount( 8 );

            cmd.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                       .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
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
                } );
            cmd.MinimalAbstractTypes.Should().HaveCount( 1 );
            cmd.MinimalAbstractTypes.Single().ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                        .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" )
                .Should().Be( "[AbstractPoco]IInput<object>" );

        }

        [Test]
        public void registering_purely_abstract_type()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IAbstractCommand ) );
            var r = TestHelper.GetSuccessfulCollectorResult( c );
            var pocoBuilder = r.CKTypeResult.PocoTypeSystemBuilder;
            var veryAbstract = pocoBuilder.FindByType<IAbstractPocoType>( typeof( IAbstractCommand ) );
            Throw.DebugAssert( veryAbstract != null );
            veryAbstract.ImplementationLess.Should().BeTrue();
        }

        [TestCase( true )]
        [TestCase( false )]
        public void explicit_registering_abstract_generic_poco( bool registerGeneric )
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.AddTypes( typeof( IS5Command ) );
            var engineResult = configuration.RunSuccessfully();
            var ts = engineResult.FirstBinPath.PocoTypeSystemBuilder;

            var primary = ts.FindByType<IPrimaryPocoType>( typeof( ITopCommand ) );
            Throw.DebugAssert( primary != null && primary.IsNullable );
            
            var iCmdGen = ts.FindGenericTypeDefinition( typeof( ICommand<> ) );
            Throw.DebugAssert( iCmdGen != null );
            iCmdGen.Instances.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                       .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>",
                    "[AbstractPoco]ICommand<ICommand<object>>",
                    "[AbstractPoco]ICommand<IAbstractCommand>",
                    "[AbstractPoco]ICommand<ICrisPoco>",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>",
                    "[AbstractPoco]ICommand<object>"
                } );

            primary.NonNullable.AbstractTypes.Select( t => t.ToString().Replace( "CK.StObj.Engine.Tests.Poco.PocoGenericTests.", "" )
                                                                       .Replace( "CK.StObj.Engine.Tests.CrisLike.", "" ) )
                .Should().BeEquivalentTo( new[]
                {
                    "[AbstractPoco]ICommand<object>",
                    "[AbstractPoco]IAbstractCommand",
                    "[AbstractPoco]ICrisPoco",
                    "[AbstractPoco]ICommand<CK.Core.IPoco>",
                    "[AbstractPoco]ICommand<ICrisPoco>",
                    "[AbstractPoco]ICommand<IAbstractCommand>",
                    "[AbstractPoco]ICommand<ICommand<object>>",
                    "[AbstractPoco]ICommand<ICommand<ICommand<object>>>"
                } );
        }


    }
}
