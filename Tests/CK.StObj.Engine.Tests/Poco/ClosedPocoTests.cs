using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using static CK.Testing.StObjEngineTestHelper;
using System.Diagnostics;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class ClosedPocoTests
    {
        [CKTypeDefiner]
        public interface ICloPoc : IClosedPoco
        {
        }

        [CKTypeSuperDefiner]
        public interface ICloPocPart : IClosedPoco
        {
        }

        public interface IAuthenticatedCloPocPart : ICloPocPart
        {
            int ActorId { get; set; }
        }

        public interface ICultureDependentCloPocPart : ICloPocPart
        {
            int XLCID { get; set; }
        }

        public interface IUserCloPoc : ICloPoc, IAuthenticatedCloPocPart
        {
            string UserName { get; set; }
        }

        public interface IDocumentCloPoc : ICloPoc, IAuthenticatedCloPocPart
        {
            string DocName { get; set; }
        }

        public interface ICultureUserCloPoc : IUserCloPoc, ICultureDependentCloPocPart
        {
        }

        readonly Type[] BaseUserAndDocumentCloPocs = new Type[]
        {
            typeof(ICloPoc), typeof(ICloPocPart),
            typeof(IAuthenticatedCloPocPart), typeof(ICultureDependentCloPocPart),
            typeof(IUserCloPoc), typeof(IDocumentCloPoc), typeof(ICultureUserCloPoc)
        };

        [TestCase( "OnlyTheFinalUserAndDocumentCloPocs" )]
        [TestCase( "AllBaseUserAndDocumentCloPocs" )]
        public void closed_poco_and_CKTypeDefiner_and_CKTypeSuperDefiner_is_the_basis_of_the_Cris_ICommand( string mode )
        {
            var c = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            if( mode == "AllBaseUserAndDocumentCloPocs" )
            {
                c.RegisterTypes( BaseUserAndDocumentCloPocs );
            }
            else
            {
                c.RegisterType( typeof( IDocumentCloPoc ) );
                c.RegisterType( typeof( ICultureUserCloPoc ) );
            }
            var all = TestHelper.CreateAutomaticServices( c );
            try
            {
                var pocoSupportResult = all.CollectorResult.CKTypeResult.PocoSupport;
                Debug.Assert( pocoSupportResult != null );
                pocoSupportResult.Should().BeSameAs( all.CollectorResult.DynamicAssembly.GetPocoSupportResult() );
                var services = all.Services;

                var dCloPoc = services.GetRequiredService<IPocoFactory<IDocumentCloPoc>>().Create();
                dCloPoc.Should().NotBeNull( "Factories work." );

                var factoryCloPoc = services.GetService<IPocoFactory<IUserCloPoc>>();
                factoryCloPoc.Should().NotBeNull();

                services.GetService<IPocoFactory<ICloPoc>>().Should().BeNull( "ICloPoc is a CKTypeDefiner." );
                services.GetService<IPocoFactory<ICloPocPart>>().Should().BeNull( "ICloPocPart is a CKTypeSuperDefiner." );
                services.GetService<IPocoFactory<IAuthenticatedCloPocPart>>().Should().BeNull( "Since ICloPocPart is a CKTypeSuperDefiner, a command part is NOT Poco." );

                pocoSupportResult.AllInterfaces.Should().HaveCount( 3 );
                pocoSupportResult.AllInterfaces.Values.Select( info => info.PocoInterface ).Should().BeEquivalentTo(
                    new[] { typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ), typeof( IUserCloPoc ) } );

                pocoSupportResult.OtherInterfaces.Keys.Should().BeEquivalentTo(
                    new[] { typeof( ICloPoc ), typeof( ICloPocPart ), typeof( IAuthenticatedCloPocPart ), typeof( ICultureDependentCloPocPart ) } );

                pocoSupportResult.OtherInterfaces[typeof( ICloPoc )].Select( info => info.ClosureInterface ).Should()
                    .BeEquivalentTo( new[] { typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ) } );
                pocoSupportResult.OtherInterfaces[typeof( ICloPoc )].Select( info => info.PrimaryInterface ).Should().BeEquivalentTo(
                    new[] { typeof( IDocumentCloPoc ), typeof( IUserCloPoc ) } );

                pocoSupportResult.OtherInterfaces[typeof( ICloPocPart )].Should().BeEquivalentTo(
                    pocoSupportResult.OtherInterfaces[typeof( ICloPoc )], "Our 2 commands have parts." );
                pocoSupportResult.OtherInterfaces[typeof( IAuthenticatedCloPocPart )].Should().BeEquivalentTo(
                    pocoSupportResult.OtherInterfaces[typeof( ICloPoc )], "Our 2 commands have IAuthenticatedCloPocPart part." );
                pocoSupportResult.OtherInterfaces[typeof( ICultureDependentCloPocPart )].Select( info => info.ClosureInterface ).Should().BeEquivalentTo(
                    new[] { typeof( ICultureUserCloPoc ) } );

                var factoryCultCloPoc = services.GetService<IPocoFactory<ICultureUserCloPoc>>();
                factoryCultCloPoc.Should().BeSameAs( factoryCloPoc );
            }
            finally
            {
                all.Services.Dispose();
            }
        }

        public interface IOther1UserCloPoc : IUserCloPoc
        {
            int OtherId1 { get; set; }
        }

        public interface IOther2UserCloPoc : ICultureUserCloPoc
        {
            int OtherId2 { get; set; }
        }

        public interface IUserFinalCloPoc : IOther1UserCloPoc, IOther2UserCloPoc
        {
        }

        [Test]
        public void not_closed_poco_commmand_are_detected()
        {
            var c = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            c.RegisterType( typeof( IOther1UserCloPoc ) );
            c.RegisterType( typeof( IOther2UserCloPoc ) );
            TestHelper.GetFailedResult( c );
        }

        [TestCase( "IUserFinalCloPoc only" )]
        [TestCase( "All commands" )]
        public void a_closed_poco_commmand_works_fine( string mode )
        {
            var c = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            if( mode == "All commands")
            {
                c.RegisterTypes( BaseUserAndDocumentCloPocs );
                c.RegisterType( typeof( IOther1UserCloPoc ) );
                c.RegisterType( typeof( IOther2UserCloPoc ) );
                c.RegisterType( typeof( IUserFinalCloPoc ) );
            }
            else
            {
                c.RegisterType( typeof( IUserFinalCloPoc ) );
            }

            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var factoryCloPoc = services.GetService<IPocoFactory<IUserCloPoc>>();
            factoryCloPoc.Should().NotBeNull();
            services.GetService<IPocoFactory<IOther1UserCloPoc>>().Should().BeSameAs( factoryCloPoc );
            services.GetService<IPocoFactory<IOther2UserCloPoc>>().Should().BeSameAs( factoryCloPoc );
            services.GetService<IPocoFactory<IUserFinalCloPoc>>().Should().BeSameAs( factoryCloPoc );
        }

        [Test]
        public void IPocoFactory_exposes_the_IsClosedPoco_and_ClosureInterface_properties()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IUserFinalCloPoc ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var fUser = services.GetRequiredService<IPocoFactory<IUserCloPoc>>();
            fUser.IsClosedPoco.Should().BeTrue();
            fUser.ClosureInterface.Should().Be( typeof( IUserFinalCloPoc ) );
        }

        public interface INotClosedByDesign : IPoco
        {
            int A { get; set; }
        }

        public interface IExtendNotClosedByDesign : INotClosedByDesign
        {
            int B { get; set; }
        }

        public interface IAnotherExtendNotClosedByDesign : INotClosedByDesign
        {
            int C { get; set; }
        }

        [Test]
        public void the_ClosureInterface_is_available_if_a_closure_interface_exists_even_if_the_Poco_is_not_a_IClosedPoco()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( INotClosedByDesign ) );
                using var services = TestHelper.CreateAutomaticServices( c ).Services;
                var f = services.GetRequiredService<IPocoFactory<INotClosedByDesign>>();
                f.IsClosedPoco.Should().BeFalse();
                f.ClosureInterface.Should().Be( typeof( INotClosedByDesign ) );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IExtendNotClosedByDesign ) );
                using var services = TestHelper.CreateAutomaticServices( c ).Services;
                var f = services.GetRequiredService<IPocoFactory<IExtendNotClosedByDesign>>();
                f.IsClosedPoco.Should().BeFalse();
                f.ClosureInterface.Should().Be( typeof( IExtendNotClosedByDesign ) );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IExtendNotClosedByDesign ), typeof( IAnotherExtendNotClosedByDesign ) );
                using var services = TestHelper.CreateAutomaticServices( c ).Services;
                var f = services.GetRequiredService<IPocoFactory<IExtendNotClosedByDesign>>();
                f.Name.Should().Be( "CK.StObj.Engine.Tests.Poco.ClosedPocoTests+INotClosedByDesign" );
                f.Interfaces.Should().HaveCount( 3 );
                f.IsClosedPoco.Should().BeFalse();
                f.ClosureInterface.Should().BeNull();
            }
        }


    }
}
