using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests;

public class MultiListImplementationTests : CommonTypes
{
    [CKTypeDefiner]
    public interface IWithList : IPoco
    {
        object List { get; }
        // Abstract read-only property that enables to check that a
        // default non nullable List has been created.
        object ConcreteList { get; }
    }

    [CKTypeDefiner]
    public interface IWithReadOnlyList : IPoco
    {
        IReadOnlyList<IAbstractBase> List { get; }
        // Abstract read-only property that enables to check that a
        // default non nullable List has been created.
        object ConcreteList { get; }
    }

    public interface IPocoWithListOfPrimary : IPoco, IWithList
    {
        new IList<IVerySimplePoco> List { get; }
        new List<IVerySimplePoco> ConcreteList { get; set; }
    }

    public interface IPocoWithListOfSecondary : IPoco, IWithList, IWithReadOnlyList
    {
        new IList<ISecondaryVerySimplePoco> List { get; }
        new List<ISecondaryVerySimplePoco> ConcreteList { get; set; }
    }

    public interface IPocoWithListOfOtherSecondary : IPoco, IWithList, IWithReadOnlyList
    {
        new IList<IOtherSecondaryVerySimplePoco> List { get; }
        new List<IOtherSecondaryVerySimplePoco> ConcreteList { get; set; }
    }

    public interface IPocoWithListOfAbstract : IPoco, IWithList, IWithReadOnlyList
    {
        new IList<IAbstract2> List { get; }
        new List<IAbstract2> ConcreteList { get; set; }
    }

    [TestCase( typeof( IPocoWithListOfPrimary ) )]
    [TestCase( typeof( IPocoWithListOfSecondary ) )]
    [TestCase( typeof( IPocoWithListOfOtherSecondary ) )]
    [TestCase( typeof( IPocoWithListOfAbstract ) )]
    public async Task IList_implementation_supports_all_the_required_types_Async( Type type )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                        typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                        type );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var p = (IWithList)d.Find( type )!.Create();

        p.List.Should()
            .BeAssignableTo<IReadOnlyList<object>>()
            .And.BeAssignableTo<IReadOnlyList<IPoco>>();
        p.ConcreteList.GetType().Name.Should().Be( "List`1" );

        if( type != typeof( IPocoWithListOfAbstract ) )
        {
            p.List.Should()
            .BeAssignableTo<IList<IVerySimplePoco>>()
            .And.BeAssignableTo<IList<ISecondaryVerySimplePoco>>()
            .And.BeAssignableTo<IList<IOtherSecondaryVerySimplePoco>>()
            .And.BeAssignableTo<IReadOnlyList<object>>()
            .And.BeAssignableTo<IReadOnlyList<IPoco>>()
            .And.BeAssignableTo<IReadOnlyList<IVerySimplePoco>>()
            .And.BeAssignableTo<IReadOnlyList<ISecondaryVerySimplePoco>>()
            .And.BeAssignableTo<IReadOnlyList<IOtherSecondaryVerySimplePoco>>();

            if( type != typeof( IPocoWithListOfPrimary ) )
            {
                p.List.Should().BeAssignableTo<IReadOnlyList<IAbstractBase>>();
                if( type == typeof( IPocoWithListOfSecondary ) )
                {
                    p.List.Should().BeAssignableTo<IReadOnlyList<IAbstract1>>();
                }
                else
                {
                    p.List.Should().BeAssignableTo<IReadOnlyList<IAbstract2>>();
                }
            }
        }
    }

    public interface IPocoWithListOfAbstractBase : IPoco
    {
        IList<IAbstractBase> List { get; }
    }

    public interface IPocoWithListOfAbstract1 : IPoco
    {
        IList<IAbstract1> List { get; }
    }

    public interface IPocoWithListOfClosed : IPoco
    {
        IList<IClosed> List { get; }
    }

    [Test]
    public async Task IList_implementation_of_Abstract_is_natively_covariant_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                        typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                        typeof( IPocoWithListOfAbstractBase ), typeof( IPocoWithListOfAbstract1 ),
                                        typeof( IAbstract1Closed ), typeof( IClosed ), typeof( IPocoWithListOfClosed ) );
        using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();

        var pBase = d.Create<IPocoWithListOfAbstractBase>();
        pBase.List.Should().BeAssignableTo<IReadOnlyList<object>>()
            .And.BeAssignableTo<IReadOnlyList<IPoco>>()
            .And.BeAssignableTo<IReadOnlyList<IAbstractBase>>();

        var pAbstract1 = d.Create<IPocoWithListOfAbstract1>();
        pAbstract1.List.Should().BeAssignableTo<IReadOnlyList<object>>()
            .And.BeAssignableTo<IReadOnlyList<IPoco>>()
            .And.BeAssignableTo<IReadOnlyList<IAbstractBase>>()
            .And.BeAssignableTo<IReadOnlyList<IAbstract1>>();

        var pClosed = d.Create<IPocoWithListOfClosed>();
        pClosed.List.Should().BeAssignableTo<IReadOnlyList<object>>()
            .And.BeAssignableTo<IReadOnlyList<IPoco>>()
            .And.BeAssignableTo<IReadOnlyList<IAbstractBase>>()
            .And.BeAssignableTo<IReadOnlyList<IAbstract1>>()
            .And.BeAssignableTo<IReadOnlyList<IAbstract1Closed>>()
            .And.BeAssignableTo<IReadOnlyList<IClosedPoco>>();
    }

    public interface IInvalid : IPocoWithListOfAbstractBase
    {
        new IList<IAbstract1> List { get; }
    }

    [Test]
    public void as_usual_writable_type_is_invariant()
    {
        // ISecondaryVerySimplePoco is required for IAbstractBase and IAbstract1 to be actually registered. 
        TestHelper.GetFailedCollectorResult( [typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IInvalid ), typeof( ISecondaryVerySimplePoco )],
            $"Property type conflict between:{Environment.NewLine}" +
            $"IList<CommonTypes.IAbstract1> CK.StObj.Engine.Tests.Poco.AbstractImplTests.MultiListImplementationTests.IInvalid.List{Environment.NewLine}" +
            $"And:{Environment.NewLine}" +
            $"IList<CommonTypes.IAbstractBase> CK.StObj.Engine.Tests.Poco.AbstractImplTests.MultiListImplementationTests.IPocoWithListOfAbstractBase.List" );
    }
}
