using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

using static CK.Testing.StObjEngineTestHelper;
using System.Diagnostics;
using CK.Testing;

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

        public readonly Type[] BaseUserAndDocumentCloPocs = new Type[]
        {
            typeof(ICloPoc), typeof(ICloPocPart),
            typeof(IAuthenticatedCloPocPart), typeof(ICultureDependentCloPocPart),
            typeof(IUserCloPoc), typeof(IDocumentCloPoc), typeof(ICultureUserCloPoc)
        };

        [TestCase( "OnlyTheFinalUserAndDocumentCloPocs" )]
        [TestCase( "AllBaseUserAndDocumentCloPocs" )]
        public void closed_poco_and_CKTypeDefiner_and_CKTypeSuperDefiner_is_the_basis_of_the_Cris_ICommand( string mode )
        {
            var c = TestHelper.CreateTypeCollector();
            if( mode == "AllBaseUserAndDocumentCloPocs" )
            {
                c.Add( BaseUserAndDocumentCloPocs );
            }
            else
            {
                c.Add( typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ) );
            }
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var pocoDirectory = auto.LoadResult.EngineResult.Groups[0].PocoTypeSystemBuilder!.PocoDirectory;
                Throw.DebugAssert( pocoDirectory != null );
                var services = auto.Services;

                var dCloPoc = services.GetRequiredService<IPocoFactory<IDocumentCloPoc>>().Create();
                dCloPoc.Should().NotBeNull( "Factories work." );

                var factoryCloPoc = services.GetService<IPocoFactory<IUserCloPoc>>();
                factoryCloPoc.Should().NotBeNull();

                services.GetService<IPocoFactory<ICloPoc>>().Should().BeNull( "ICloPoc is a CKTypeDefiner." );
                services.GetService<IPocoFactory<ICloPocPart>>().Should().BeNull( "ICloPocPart is a CKTypeSuperDefiner." );
                services.GetService<IPocoFactory<IAuthenticatedCloPocPart>>().Should().BeNull( "Since ICloPocPart is a CKTypeSuperDefiner, a command part is NOT Poco." );

                pocoDirectory.AllInterfaces.Should().HaveCount( 3 );
                pocoDirectory.AllInterfaces.Values.Select( info => info.PocoInterface ).Should().BeEquivalentTo(
                    new[] { typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ), typeof( IUserCloPoc ) } );

                pocoDirectory.OtherInterfaces.Keys.Should().BeEquivalentTo(
                    new[] { typeof( IClosedPoco ), typeof( ICloPoc ), typeof( ICloPocPart ), typeof( IAuthenticatedCloPocPart ), typeof( ICultureDependentCloPocPart ) } );

                pocoDirectory.OtherInterfaces[typeof( ICloPoc )].Select( info => info.ClosureInterface ).Should()
                    .BeEquivalentTo( new[] { typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ) } );
                pocoDirectory.OtherInterfaces[typeof( ICloPoc )].Select( info => info.PrimaryInterface.PocoInterface ).Should().BeEquivalentTo(
                    new[] { typeof( IDocumentCloPoc ), typeof( IUserCloPoc ) } );

                pocoDirectory.OtherInterfaces[typeof( ICloPocPart )].Should().BeEquivalentTo(
                    pocoDirectory.OtherInterfaces[typeof( ICloPoc )], "Our 2 commands have parts." );
                pocoDirectory.OtherInterfaces[typeof( IAuthenticatedCloPocPart )].Should().BeEquivalentTo(
                    pocoDirectory.OtherInterfaces[typeof( ICloPoc )], "Our 2 commands have IAuthenticatedCloPocPart part." );
                pocoDirectory.OtherInterfaces[typeof( ICultureDependentCloPocPart )].Select( info => info.ClosureInterface ).Should().BeEquivalentTo(
                    new[] { typeof( ICultureUserCloPoc ) } );

                var factoryCultCloPoc = services.GetService<IPocoFactory<ICultureUserCloPoc>>();
                factoryCultCloPoc.Should().BeSameAs( factoryCloPoc );
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
            var c = TestHelper.CreateTypeCollector( typeof( IOther1UserCloPoc ), typeof( IOther2UserCloPoc ) );
            TestHelper.GetFailedCollectorResult( c, "must be closed but none of these interfaces covers the other ones" );
        }

        [TestCase( "IUserFinalCloPoc only" )]
        [TestCase( "All commands" )]
        public void a_closed_poco_commmand_works_fine( string mode )
        {
            var c = TestHelper.CreateTypeCollector();
            if( mode == "All commands")
            {
                c.Add( BaseUserAndDocumentCloPocs ).Add( typeof( IOther1UserCloPoc ), typeof( IOther2UserCloPoc ), typeof( IUserFinalCloPoc ) );
            }
            else
            {
                c.Add( typeof( IUserFinalCloPoc ) );
            }

            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
            var factoryCloPoc = auto.Services.GetService<IPocoFactory<IUserCloPoc>>();
            factoryCloPoc.Should().NotBeNull();
            auto.Services.GetService<IPocoFactory<IOther1UserCloPoc>>().Should().BeSameAs( factoryCloPoc );
            auto.Services.GetService<IPocoFactory<IOther2UserCloPoc>>().Should().BeSameAs( factoryCloPoc );
            auto.Services.GetService<IPocoFactory<IUserFinalCloPoc>>().Should().BeSameAs( factoryCloPoc );
        }

        [Test]
        public void IPocoFactory_exposes_the_IsClosedPoco_and_ClosureInterface_properties()
        {
            var c = TestHelper.CreateTypeCollector( typeof( IUserFinalCloPoc ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
            var fUser = auto.Services.GetRequiredService<IPocoFactory<IUserCloPoc>>();
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
                var c = TestHelper.CreateTypeCollector( typeof( INotClosedByDesign ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var f = auto.Services.GetRequiredService<IPocoFactory<INotClosedByDesign>>();
                f.IsClosedPoco.Should().BeFalse();
                f.ClosureInterface.Should().Be( typeof( INotClosedByDesign ) );
            }
            {
                var c = TestHelper.CreateTypeCollector( typeof( IExtendNotClosedByDesign ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var f = auto.Services.GetRequiredService<IPocoFactory<IExtendNotClosedByDesign>>();
                f.IsClosedPoco.Should().BeFalse();
                f.ClosureInterface.Should().Be( typeof( IExtendNotClosedByDesign ) );
            }
            {
                var c = TestHelper.CreateTypeCollector( typeof( IExtendNotClosedByDesign ), typeof( IAnotherExtendNotClosedByDesign ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );
                var f = auto.Services.GetRequiredService<IPocoFactory<IExtendNotClosedByDesign>>();
                f.Name.Should().Be( "CK.StObj.Engine.Tests.Poco.ClosedPocoTests.INotClosedByDesign" );
                f.Interfaces.Should().HaveCount( 3 );
                f.IsClosedPoco.Should().BeFalse();
                f.ClosureInterface.Should().BeNull();
            }
        }


    }
}
