using Shouldly;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service.TypeCollector;


[TestFixture]
public class BasicTests : TypeCollectorTestsBase
{
    // Test with an alternate IScopedAutoService that is not the
    // "official" CK.Core.IScopedAutoService from CK.StObj.Model.
    public interface IScopedAutoService { }

    public interface IServiceRegistered : IScopedAutoService
    {
    }

    public interface IServiceNotRegisteredSinceNotImplemented : IServiceRegistered
    {
    }

    public class ServiceRegisteredImpl : IServiceRegistered
    {
    }

    public class ServiceNotRegisteredImpl : ServiceRegisteredImpl, IServiceNotRegisteredSinceNotImplemented
    {
    }

    [Test]
    public void registering_service_registers_specialized_interfaces_and_base_impl_but_mask_them()
    {
        var r = CheckSuccess( collector => collector.RegisterType( TestHelper.Monitor, typeof( ServiceNotRegisteredImpl ) ) );
        var interfaces = r.AutoServices.LeafInterfaces;
        interfaces.Count.ShouldBe( 1 );
        var iSpec = interfaces[0];
        var iBase = iSpec.Interfaces[0];
        iBase.Type.ShouldBe( typeof( IServiceRegistered ) );
        iBase.SpecializationDepth.ShouldBe( 0 );
        iBase.IsSpecialized.ShouldBeTrue();
        iBase.Interfaces.ShouldBeEmpty();
        iSpec.Type.ShouldBe( typeof( IServiceNotRegisteredSinceNotImplemented ) );
        iSpec.SpecializationDepth.ShouldBe( 1 );
        iSpec.IsSpecialized.ShouldBeFalse();
        iSpec.Interfaces.ShouldHaveSingleItem().ShouldBe( iBase );
        var classes = r.AutoServices.RootClasses;
        classes.Count.ShouldBe( 1 );
        var cBase = classes[0];
        cBase.ClassType.ShouldBe( typeof( ServiceRegisteredImpl ) );
        cBase.TypeInfo.IsSpecialized.ShouldBeTrue();
        var cSpec = cBase.MostSpecialized;
        Debug.Assert( cSpec != null );
        cBase.Specializations.ShouldHaveSingleItem().ShouldBe( cSpec );
        cSpec.ClassType.ShouldBe( typeof( ServiceNotRegisteredImpl ) );
        cSpec.Generalization.ShouldBeSameAs( cBase );
        cSpec.Interfaces.ShouldBe( new[] { iBase, iSpec } );
    }

}
