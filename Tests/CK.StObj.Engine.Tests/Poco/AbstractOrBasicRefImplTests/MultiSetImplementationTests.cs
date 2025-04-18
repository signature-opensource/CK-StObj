using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests;

public class MultiSetImplementationTests : CommonTypes
{
    [CKTypeDefiner]
    public interface IWithSet : IPoco
    {
        object Set { get; }
    }

    [CKTypeDefiner]
    public interface IWithReadOnlySet : IPoco
    {
        IReadOnlySet<object> Set { get; }
    }

    public interface IPocoWithSetOfString : IPoco, IWithSet
    {
        new ISet<string> Set { get; }
    }

    public interface IPocoWithSetOfGuid : IPoco, IWithSet, IWithReadOnlySet
    {
        new ISet<Guid> Set { get; }
    }

    public record struct ROCompliant( string Name, int Power );

    public interface IPocoWithSetOfReadOnlyCompliantNamedRecord : IPoco, IWithSet, IWithReadOnlySet
    {
        new ISet<ROCompliant> Set { get; }
    }

    public interface IPocoWithSetOfReadOnlyCompliantAnonymousRecord : IPoco, IWithSet, IWithReadOnlySet
    {
        new ISet<(string Name, Guid Id)> Set { get; }
    }

    [TestCase( typeof( IPocoWithSetOfString ) )]
    [TestCase( typeof( IPocoWithSetOfGuid ) )]
    [TestCase( typeof( IPocoWithSetOfReadOnlyCompliantNamedRecord ) )]
    [TestCase( typeof( IPocoWithSetOfReadOnlyCompliantAnonymousRecord ) )]
    public async Task ISet_implementation_supports_all_the_required_types_Async( Type type )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( type );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var p = (IWithSet)d.Find( type )!.Create();

        p.Set.ShouldBeAssignableTo<IReadOnlySet<object>>();

        if( type == typeof( IPocoWithSetOfString ) )
        {
            ((IPocoWithSetOfString)p).Set.Add( "Here" );
            ((IEnumerable<object>)p.Set).ShouldContain( "Here" );
        }
        if( type == typeof( IPocoWithSetOfGuid ) )
        {
            ((IPocoWithSetOfGuid)p).Set.Add( Guid.Empty );
            ((IEnumerable<Guid>)p.Set).ShouldContain( Guid.Empty );
        }
    }

    [CKTypeDefiner]
    public interface IAbstractBasicRefSets : IPoco
    {
        IReadOnlySet<object> StringSet { get; }
        IReadOnlySet<object> ExtendedCultureInfoSet { get; }
        IReadOnlySet<object> NormalizedCultureInfoSet { get; }
    }

    public interface IBasicRefSets : IAbstractBasicRefSets
    {
        new ISet<string> StringSet { get; }
        new ISet<ExtendedCultureInfo> ExtendedCultureInfoSet { get; }
        new ISet<NormalizedCultureInfo> NormalizedCultureInfoSet { get; }
    }

    [Test]
    public async Task ISet_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required_for_basic_ref_types_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractBasicRefSets ), typeof( IBasicRefSets ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var pBase = d.Create<IBasicRefSets>();
        pBase.StringSet.ShouldNotBeNull();
        pBase.ExtendedCultureInfoSet.ShouldNotBeNull();
        pBase.NormalizedCultureInfoSet.ShouldNotBeNull();
        pBase.NormalizedCultureInfoSet.ShouldBeAssignableTo<IReadOnlySet<ExtendedCultureInfo>>();
    }
}
