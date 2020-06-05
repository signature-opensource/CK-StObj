using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using static CK.Testing.StObjEngineTestHelper;
using System.Diagnostics;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class PocoCommandTests
    {
        [CKTypeDefiner]
        public interface ICommand : IClosedPoco
        {
        }

        [CKTypeSuperDefiner]
        public interface ICommandPart : IClosedPoco
        {
        }

        public interface IAuthenticatedCommandPart : ICommandPart
        {
            int ActorId { get; set; }
        }

        public interface ICultureDependentCommandPart : ICommandPart
        {
            int XLCID { get; set; }
        }

        public interface ICreateUserCommand : ICommand, IAuthenticatedCommandPart
        {
            string UserName { get; set; }
        }

        public interface ICreateDocumentCommand : ICommand, IAuthenticatedCommandPart
        {
            string DocName { get; set; }
        }

        public interface ICultureCreateUserCommand : ICreateUserCommand, ICultureDependentCommandPart
        {
        }

        Type[] BaseUserAndDocumentCommands = new Type[]
        {
            typeof(ICommand), typeof(ICommandPart),
            typeof(IAuthenticatedCommandPart), typeof(ICultureDependentCommandPart),
            typeof(ICreateUserCommand), typeof(ICreateDocumentCommand), typeof(ICultureCreateUserCommand)
        };

        [TestCase( "OnlyTheFinalUserAndDocumentCommands" )]
        [TestCase( "AllBaseUserAndDocumentCommands" )]
        public void closed_poco_and_CKTypeDefiner_and_CKTypeSuperDefiner_is_the_basis_of_the_Cris_Command( string mode )
        {
            var c = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            if( mode == "AllBaseUserAndDocumentCommands" )
            {
                c.RegisterTypes( BaseUserAndDocumentCommands );
            }
            else
            {
                c.RegisterType( typeof( ICreateDocumentCommand ) );
                c.RegisterType( typeof( ICultureCreateUserCommand ) );
            }
            var all = TestHelper.GetAutomaticServices( c );
            var pocoSupportResult = all.Result.CKTypeResult.PocoSupport;
            Debug.Assert( pocoSupportResult != null );
            pocoSupportResult.Should().BeSameAs( all.Result.DynamicAssembly.GetPocoSupportResult() );
            var services = all.Services;

            var dCommand = services.GetService<IPocoFactory<ICreateDocumentCommand>>().Create();
            dCommand.Should().NotBeNull( "Factories work." );

            var factoryCommand = services.GetService<IPocoFactory<ICreateUserCommand>>();
            factoryCommand.Should().NotBeNull();

            services.GetService<IPocoFactory<ICommand>>().Should().BeNull( "ICommand is a CKTypeDefiner." );
            services.GetService<IPocoFactory<ICommandPart>>().Should().BeNull( "ICommandPart is a CKTypeSuperDefiner." );
            services.GetService<IPocoFactory<IAuthenticatedCommandPart>>().Should().BeNull( "Since ICommandPart is a CKTypeSuperDefiner, a command part is NOT Poco." );

            pocoSupportResult.AllInterfaces.Should().HaveCount( 3 );
            pocoSupportResult.AllInterfaces.Values.Select( info => info.PocoInterface ).Should().BeEquivalentTo( typeof( ICreateDocumentCommand ), typeof( ICultureCreateUserCommand ), typeof( ICreateUserCommand ) );
            pocoSupportResult.OtherInterfaces.Keys.Should().BeEquivalentTo( typeof(ICommand), typeof(ICommandPart), typeof(IAuthenticatedCommandPart), typeof(ICultureDependentCommandPart) );

            pocoSupportResult.OtherInterfaces[typeof( ICommand )].Select( info => info.ClosureInterface ).Should().BeEquivalentTo( typeof( ICreateDocumentCommand ), typeof( ICultureCreateUserCommand ) );
            pocoSupportResult.OtherInterfaces[typeof( ICommandPart )].Should().BeEquivalentTo( pocoSupportResult.OtherInterfaces[typeof( ICommand )], "Our 2 commands have parts." );
            pocoSupportResult.OtherInterfaces[typeof( IAuthenticatedCommandPart )].Should().BeEquivalentTo( pocoSupportResult.OtherInterfaces[typeof( ICommand )], "Our 2 commands have IAuthenticatedCommandPart part." );
            pocoSupportResult.OtherInterfaces[typeof( ICultureDependentCommandPart )].Select( info => info.ClosureInterface ).Should().BeEquivalentTo( typeof( ICultureCreateUserCommand ) );

            var factoryCultCommand = services.GetService<IPocoFactory<ICultureCreateUserCommand>>();
            factoryCultCommand.Should().BeSameAs( factoryCommand );
        }

        public interface IOther1CreateUserCommand : ICreateUserCommand
        {
            int OtherId1 { get; }
        }

        public interface IOther2CreateUserCommand : ICultureCreateUserCommand
        {
            int OtherId2 { get; }
        }

        public interface ICreateUserFinalCommand : IOther1CreateUserCommand, IOther2CreateUserCommand
        {
        }

        [Test]
        public void not_closed_poco_commmand_are_detected()
        {
            var c = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            c.RegisterType( typeof( IOther1CreateUserCommand ) );
            c.RegisterType( typeof( IOther2CreateUserCommand ) );
            TestHelper.GetFailedResult( c );
        }

        [TestCase( "ICreateUserFinalCommand only" )]
        [TestCase( "All commands" )]
        public void a_closed_poco_commmand_works_fine( string mode )
        {
            var c = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            if( mode == "All commands")
            {
                c.RegisterTypes( BaseUserAndDocumentCommands );
                c.RegisterType( typeof( IOther1CreateUserCommand ) );
                c.RegisterType( typeof( IOther2CreateUserCommand ) );
                c.RegisterType( typeof( ICreateUserFinalCommand ) );
            }
            else
            {
                c.RegisterType( typeof( ICreateUserFinalCommand ) );
            }

            var services = TestHelper.GetAutomaticServices( c ).Services;
            var factoryCommand = services.GetService<IPocoFactory<ICreateUserCommand>>();
            factoryCommand.Should().NotBeNull();
            services.GetService<IPocoFactory<IOther1CreateUserCommand>>().Should().BeSameAs( factoryCommand );
            services.GetService<IPocoFactory<IOther2CreateUserCommand>>().Should().BeSameAs( factoryCommand );
            services.GetService<IPocoFactory<ICreateUserFinalCommand>>().Should().BeSameAs( factoryCommand );
        }

    }
}
