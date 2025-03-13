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

public class MultiDictionaryImplementationTests : CommonTypes
{
    [CKTypeDefiner]
    public interface IWithDictionary : IPoco
    {
        object Dictionary { get; }
        // Abstract read-only property that enables to check that a
        // default non nullable Dictionary has been created.
        object ConcreteDictionary { get; }
    }

    [CKTypeDefiner]
    public interface IWithReadOnlyDictionary : IPoco
    {
        IReadOnlyDictionary<int, IAbstractBase> Dictionary { get; }
        object ConcreteDictionary { get; }
    }

    public interface IPocoWithDictionaryOfPrimary : IPoco, IWithDictionary
    {
        new IDictionary<int, IVerySimplePoco> Dictionary { get; }
        new Dictionary<int, IVerySimplePoco> ConcreteDictionary { get; set; }
    }

    public interface IPocoWithDictionaryOfSecondary : IPoco, IWithDictionary, IWithReadOnlyDictionary
    {
        new IDictionary<int, ISecondaryVerySimplePoco> Dictionary { get; }
        new Dictionary<int, ISecondaryVerySimplePoco> ConcreteDictionary { get; set; }
    }

    public interface IPocoWithDictionaryOfOtherSecondary : IPoco, IWithDictionary, IWithReadOnlyDictionary
    {
        new IDictionary<int, IOtherSecondaryVerySimplePoco> Dictionary { get; }
        new Dictionary<int, IOtherSecondaryVerySimplePoco> ConcreteDictionary { get; set; }
    }

    [TestCase( typeof( IPocoWithDictionaryOfPrimary ) )]
    [TestCase( typeof( IPocoWithDictionaryOfSecondary ) )]
    [TestCase( typeof( IPocoWithDictionaryOfOtherSecondary ) )]
    public async Task IDictionary_implementation_supports_all_the_required_types_Async( Type type )
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                        typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                        type );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var p = (IWithDictionary)d.Find( type )!.Create();

        p.Dictionary.ShouldBeAssignableTo<IDictionary<int, IVerySimplePoco>>();
        p.Dictionary.ShouldBeAssignableTo<IDictionary<int, ISecondaryVerySimplePoco>>();
        p.Dictionary.ShouldBeAssignableTo<IDictionary<int, IOtherSecondaryVerySimplePoco>>();
        p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, object>>();
        p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, IPoco>>();
        p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, IVerySimplePoco>>();
        p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, ISecondaryVerySimplePoco>>();
        p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, IOtherSecondaryVerySimplePoco>>();

        p.ConcreteDictionary.GetType().Name.ShouldBe( "Dictionary`2" );

        if( type != typeof( IPocoWithDictionaryOfPrimary ) )
        {
            p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, IAbstractBase>>();
            if( type == typeof( IPocoWithDictionaryOfSecondary ) )
            {
                p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, IAbstract1>>();
            }
            else
            {
                p.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<int, IAbstract2>>();
            }
        }
    }

    public interface IPocoWithDictionaryOfAbstractBase : IPoco
    {
        IDictionary<string, IAbstractBase> Dictionary { get; }
    }

    public interface IPocoWithDictionaryOfAbstract1 : IPoco
    {
        IDictionary<string, IAbstract1> Dictionary { get; }
    }

    public interface IPocoWithDictionaryOfClosed : IPoco
    {
        IDictionary<string, IClosed> Dictionary { get; }
    }

    [Test]
    public async Task IDictionary_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                        typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                        typeof( IPocoWithDictionaryOfAbstractBase ), typeof( IPocoWithDictionaryOfAbstract1 ),
                                        typeof( IAbstract1Closed ), typeof( IClosed ), typeof( IPocoWithDictionaryOfClosed ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();

        var pBase = d.Create<IPocoWithDictionaryOfAbstractBase>();
        pBase.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
        pBase.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IPoco>>();
        pBase.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IAbstractBase>>();

        var pAbstract1 = d.Create<IPocoWithDictionaryOfAbstract1>();
        pAbstract1.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
        pAbstract1.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IPoco>>();
        pAbstract1.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IAbstractBase>>();
        pAbstract1.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IAbstract1>>();

        var pClosed = d.Create<IPocoWithDictionaryOfClosed>();
        pClosed.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>();
        pClosed.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IPoco>>();
        pClosed.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IAbstractBase>>();
        pClosed.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IAbstract1>>();
        pClosed.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IAbstract1Closed>>();
        pClosed.Dictionary.ShouldBeAssignableTo<IReadOnlyDictionary<string, IClosedPoco>>();
    }

    [CKTypeDefiner]
    public interface IAbstractBasicRefDic : IPoco
    {
        IReadOnlyDictionary<int, object> StringDic { get; }
        IReadOnlyDictionary<int, object> ExtendedCultureInfoDic { get; }
        IReadOnlyDictionary<int, object> NormalizedCultureInfoDic { get; }
        IReadOnlyDictionary<int, object> MCStringDic { get; }
        IReadOnlyDictionary<int, object> CodeStringDic { get; }
    }

    public interface IBasicRefDics : IAbstractBasicRefDic
    {
        new IDictionary<int, string> StringDic { get; }
        new IDictionary<int, ExtendedCultureInfo> ExtendedCultureInfoDic { get; }
        new IDictionary<int, NormalizedCultureInfo> NormalizedCultureInfoDic { get; }
        new IDictionary<int, MCString> MCStringDic { get; }
        new IDictionary<int, CodeString> CodeStringDic { get; }
    }

    [Test]
    public async Task IDictionary_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required_for_basic_ref_types_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( IAbstractBasicRefDic ), typeof( IBasicRefDics ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var d = auto.Services.GetRequiredService<PocoDirectory>();
        var pBase = d.Create<IBasicRefDics>();
        pBase.StringDic.ShouldNotBeNull();
        pBase.ExtendedCultureInfoDic.ShouldNotBeNull();
        pBase.NormalizedCultureInfoDic.ShouldNotBeNull();
        pBase.MCStringDic.ShouldNotBeNull();
        pBase.CodeStringDic.ShouldNotBeNull();
        pBase.NormalizedCultureInfoDic.ShouldBeAssignableTo<IReadOnlyDictionary<int, ExtendedCultureInfo>>();
    }
}
