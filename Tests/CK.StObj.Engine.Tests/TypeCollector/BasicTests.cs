using FluentAssertions;
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

    [TestCase( "ExcludingSpecializedType" )]
    [TestCase( "NotRegisteringSpecializedType" )]
    public void service_interface_without_at_least_one_impl_are_ignored( string mode )
    {
        var r = CheckSuccess( c =>
        {
            c.RegisterType( TestHelper.Monitor, typeof( ServiceRegisteredImpl ) );
            if( mode == "ExcludingSpecializedType" )
            {
                c.RegisterType( TestHelper.Monitor, typeof( ServiceNotRegisteredImpl ) );
            }
        }, mode == "ExcludingSpecializedType"
                        ? CreateCKTypeCollector( t => t != typeof( ServiceNotRegisteredImpl ) )
                        : CreateCKTypeCollector() );

        var interfaces = r.AutoServices.LeafInterfaces;
        interfaces.Should().HaveCount( 1 );
        interfaces[0].Type.Should().Be( typeof( IServiceRegistered ) );
        var classes = r.AutoServices.RootClasses;
        classes.Should().HaveCount( 1 );
        classes[0].TypeInfo.IsExcluded.Should().BeFalse();
        classes[0].Generalization.Should().BeNull();
        classes[0].TypeInfo.IsSpecialized.Should().BeFalse();
        classes[0].ClassType.Should().Be( typeof( ServiceRegisteredImpl ) );
        classes[0].Interfaces.Should().BeEquivalentTo( interfaces );
    }

    [Test]
    public void registering_service_registers_specialized_interfaces_and_base_impl_but_mask_them()
    {
        var r = CheckSuccess( collector => collector.RegisterType( TestHelper.Monitor, typeof( ServiceNotRegisteredImpl ) ) );
        var interfaces = r.AutoServices.LeafInterfaces;
        interfaces.Should().HaveCount( 1 );
        var iSpec = interfaces[0];
        var iBase = iSpec.Interfaces[0];
        iBase.Type.Should().Be( typeof( IServiceRegistered ) );
        iBase.SpecializationDepth.Should().Be( 0 );
        iBase.IsSpecialized.Should().BeTrue();
        iBase.Interfaces.Should().BeEmpty();
        iSpec.Type.Should().Be( typeof( IServiceNotRegisteredSinceNotImplemented ) );
        iSpec.SpecializationDepth.Should().Be( 1 );
        iSpec.IsSpecialized.Should().BeFalse();
        iSpec.Interfaces.Should().ContainSingle().And.Contain( iBase );
        var classes = r.AutoServices.RootClasses;
        classes.Should().HaveCount( 1 );
        var cBase = classes[0];
        cBase.ClassType.Should().Be( typeof( ServiceRegisteredImpl ) );
        cBase.TypeInfo.IsSpecialized.Should().BeTrue();
        var cSpec = cBase.MostSpecialized;
        Debug.Assert( cSpec != null );
        cBase.Specializations.Should().ContainSingle().And.Contain( cSpec );
        cSpec.ClassType.Should().Be( typeof( ServiceNotRegisteredImpl ) );
        cSpec.Generalization.Should().BeSameAs( cBase );
        cSpec.Interfaces.Should().BeEquivalentTo( new[] { iBase, iSpec } );
    }

}
