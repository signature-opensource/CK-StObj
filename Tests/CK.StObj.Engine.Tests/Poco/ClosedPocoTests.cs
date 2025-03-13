using CK.Core;
using CK.Setup;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco;

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

    public readonly Type[] BaseUserAndDocumentCloPocs =
    [
        typeof( ICloPoc ),
        typeof( ICloPocPart ),
        typeof( IAuthenticatedCloPocPart ),
        typeof( ICultureDependentCloPocPart ),
        typeof( IUserCloPoc ),
        typeof( IDocumentCloPoc ),
        typeof( ICultureUserCloPoc )
    ];

    [TestCase( "OnlyTheFinalUserAndDocumentCloPocs" )]
    [TestCase( "AllBaseUserAndDocumentCloPocs" )]
    public async Task closed_poco_and_CKTypeDefiner_and_CKTypeSuperDefiner_is_the_basis_of_the_Cris_ICommand_Async( string mode )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        if( mode == "AllBaseUserAndDocumentCloPocs" )
        {
            configuration.FirstBinPath.Types.Add( BaseUserAndDocumentCloPocs );
        }
        else
        {
            configuration.FirstBinPath.Types.Add( typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ) );
        }
        var engineResult = await configuration.RunAsync();

        var pocoDirectory = engineResult.FirstBinPath.PocoTypeSystemBuilder.PocoDirectory;
        Throw.DebugAssert( pocoDirectory != null );

        await using var auto = engineResult.CreateAutomaticServices();
        var services = auto.Services;

        var dCloPoc = services.GetRequiredService<IPocoFactory<IDocumentCloPoc>>().Create();
        dCloPoc.ShouldNotBeNull( "Factories work." );

        var factoryCloPoc = services.GetService<IPocoFactory<IUserCloPoc>>();
        factoryCloPoc.ShouldNotBeNull();

        services.GetService<IPocoFactory<ICloPoc>>().ShouldBeNull( "ICloPoc is a CKTypeDefiner." );
        services.GetService<IPocoFactory<ICloPocPart>>().ShouldBeNull( "ICloPocPart is a CKTypeSuperDefiner." );
        services.GetService<IPocoFactory<IAuthenticatedCloPocPart>>().ShouldBeNull( "Since ICloPocPart is a CKTypeSuperDefiner, a command part is NOT Poco." );

        pocoDirectory.AllInterfaces.Count.ShouldBe( 3 );
        pocoDirectory.AllInterfaces.Values.Select( info => info.PocoInterface ).ShouldBe(
            new[] { typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc ), typeof( IUserCloPoc ) } );

        pocoDirectory.OtherInterfaces.Keys.ShouldBe(
            new[] { typeof( IClosedPoco ), typeof( ICloPoc ), typeof( ICloPocPart ), typeof( IAuthenticatedCloPocPart ), typeof( ICultureDependentCloPocPart ) } );

        pocoDirectory.OtherInterfaces[typeof( ICloPoc )].Select( info => info.ClosureInterface )
            .ShouldBe( [typeof( IDocumentCloPoc ), typeof( ICultureUserCloPoc )], ignoreOrder: true );
        pocoDirectory.OtherInterfaces[typeof( ICloPoc )].Select( info => info.PrimaryInterface.PocoInterface )
            .ShouldBe( [typeof( IDocumentCloPoc ), typeof( IUserCloPoc )], ignoreOrder: true );

        pocoDirectory.OtherInterfaces[typeof( ICloPocPart )].ShouldBe(
            pocoDirectory.OtherInterfaces[typeof( ICloPoc )], "Our 2 commands have parts." );
        pocoDirectory.OtherInterfaces[typeof( IAuthenticatedCloPocPart )].ShouldBe(
            pocoDirectory.OtherInterfaces[typeof( ICloPoc )], "Our 2 commands have IAuthenticatedCloPocPart part." );
        pocoDirectory.OtherInterfaces[typeof( ICultureDependentCloPocPart )].Select( info => info.ClosureInterface ).ShouldBe(
            new[] { typeof( ICultureUserCloPoc ) } );

        var factoryCultCloPoc = services.GetService<IPocoFactory<ICultureUserCloPoc>>();
        factoryCultCloPoc.ShouldBeSameAs( factoryCloPoc );
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
        var c = new[] { typeof( IOther1UserCloPoc ), typeof( IOther2UserCloPoc ) };
        TestHelper.GetFailedCollectorResult( c, "must be closed but none of these interfaces covers the other ones" );
    }

    [TestCase( "IUserFinalCloPoc only" )]
    [TestCase( "All commands" )]
    public async Task a_closed_poco_commmand_works_fine_Async( string mode )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        if( mode == "All commands" )
        {
            configuration.FirstBinPath.Types.Add( BaseUserAndDocumentCloPocs );
            configuration.FirstBinPath.Types.Add( typeof( IOther1UserCloPoc ), typeof( IOther2UserCloPoc ), typeof( IUserFinalCloPoc ) );
        }
        else
        {
            configuration.FirstBinPath.Types.Add( typeof( IUserFinalCloPoc ) );
        }

        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();
        var factoryCloPoc = auto.Services.GetService<IPocoFactory<IUserCloPoc>>();
        factoryCloPoc.ShouldNotBeNull();
        auto.Services.GetService<IPocoFactory<IOther1UserCloPoc>>().ShouldBeSameAs( factoryCloPoc );
        auto.Services.GetService<IPocoFactory<IOther2UserCloPoc>>().ShouldBeSameAs( factoryCloPoc );
        auto.Services.GetService<IPocoFactory<IUserFinalCloPoc>>().ShouldBeSameAs( factoryCloPoc );
    }

    [Test]
    public async Task IPocoFactory_exposes_the_IsClosedPoco_and_ClosureInterface_properties_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IUserFinalCloPoc ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var fUser = auto.Services.GetRequiredService<IPocoFactory<IUserCloPoc>>();
        fUser.IsClosedPoco.ShouldBeTrue();
        fUser.ClosureInterface.ShouldBe( typeof( IUserFinalCloPoc ) );
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
    public async Task the_ClosureInterface_is_available_if_a_closure_interface_exists_even_if_the_Poco_is_not_a_IClosedPoco_Async()
    {
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( INotClosedByDesign ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            var f = auto.Services.GetRequiredService<IPocoFactory<INotClosedByDesign>>();
            f.IsClosedPoco.ShouldBeFalse();
            f.ClosureInterface.ShouldBe( typeof( INotClosedByDesign ) );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IExtendNotClosedByDesign ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            var f = auto.Services.GetRequiredService<IPocoFactory<IExtendNotClosedByDesign>>();
            f.IsClosedPoco.ShouldBeFalse();
            f.ClosureInterface.ShouldBe( typeof( IExtendNotClosedByDesign ) );
        }
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IExtendNotClosedByDesign ), typeof( IAnotherExtendNotClosedByDesign ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            var f = auto.Services.GetRequiredService<IPocoFactory<IExtendNotClosedByDesign>>();
            f.Name.ShouldBe( "CK.StObj.Engine.Tests.Poco.ClosedPocoTests.INotClosedByDesign" );
            f.Interfaces.Count.ShouldBe( 3 );
            f.IsClosedPoco.ShouldBeFalse();
            f.ClosureInterface.ShouldBeNull();
        }
    }


}
