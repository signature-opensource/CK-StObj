using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests
{
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
            IReadOnlyDictionary<int,IAbstractBase> Dictionary { get; }
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
        public void IDictionary_implementation_supports_all_the_required_types( Type type )
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                            typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                            type );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var p = (IWithDictionary)d.Find( type )!.Create();

            p.Dictionary.Should().BeAssignableTo<IDictionary<int, IVerySimplePoco>>()
                .And.BeAssignableTo<IDictionary<int, ISecondaryVerySimplePoco>>()
                .And.BeAssignableTo<IDictionary<int, IOtherSecondaryVerySimplePoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<int, object>>()
                .And.BeAssignableTo<IReadOnlyDictionary<int, IPoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<int, IVerySimplePoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<int, ISecondaryVerySimplePoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<int, IOtherSecondaryVerySimplePoco>>();
            p.ConcreteDictionary.GetType().Name.Should().Be( "Dictionary`2" );

            if( type != typeof( IPocoWithDictionaryOfPrimary ) )
            {
                p.Dictionary.Should().BeAssignableTo<IReadOnlyDictionary<int, IAbstractBase>>();
                if( type == typeof( IPocoWithDictionaryOfSecondary ) )
                {
                    p.Dictionary.Should().BeAssignableTo<IReadOnlyDictionary<int, IAbstract1>>();
                }
                else
                {
                    p.Dictionary.Should().BeAssignableTo<IReadOnlyDictionary<int, IAbstract2>>();
                }
            }
        }

        public interface IPocoWithDictionaryOfAbstractBase : IPoco
        {
            IDictionary<string,IAbstractBase> Dictionary { get; }
        }

        public interface IPocoWithDictionaryOfAbstract1 : IPoco
        {
            IDictionary<string,IAbstract1> Dictionary { get; }
        }

        public interface IPocoWithDictionaryOfClosed : IPoco
        {
            IDictionary<string,IClosed> Dictionary { get; }
        }

        [Test]
        public void IDictionary_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                            typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                            typeof( IPocoWithDictionaryOfAbstractBase ), typeof( IPocoWithDictionaryOfAbstract1 ),
                                            typeof( IAbstract1Closed ), typeof( IClosed ), typeof( IPocoWithDictionaryOfClosed ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();

            var pBase = d.Create<IPocoWithDictionaryOfAbstractBase>();
            pBase.Dictionary.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IPoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IAbstractBase>>();

            var pAbstract1 = d.Create<IPocoWithDictionaryOfAbstract1>();
            pAbstract1.Dictionary.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IPoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IAbstractBase>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IAbstract1>>();

            var pClosed = d.Create<IPocoWithDictionaryOfClosed>();
            pClosed.Dictionary.Should().BeAssignableTo<IReadOnlyDictionary<string, object>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IPoco>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IAbstractBase>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IAbstract1>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IAbstract1Closed>>()
                .And.BeAssignableTo<IReadOnlyDictionary<string, IClosedPoco>>();
        }

        [CKTypeDefiner]
        public interface IAbstractBasicRefDic: IPoco
        {
            IReadOnlyDictionary<int,object> StringDic { get; }
            IReadOnlyDictionary<int,object> ExtendedCultureInfoDic { get; }
            IReadOnlyDictionary<int,object> NormalizedCultureInfoDic { get; }
            IReadOnlyDictionary<int,object> MCStringDic { get; }
            IReadOnlyDictionary<int,object> CodeStringDic { get; }
        }

        public interface IBasicRefDics : IAbstractBasicRefDic
        {
            new IDictionary<int,string> StringDic { get; }
            new IDictionary<int,ExtendedCultureInfo> ExtendedCultureInfoDic { get; }
            new IDictionary<int,NormalizedCultureInfo> NormalizedCultureInfoDic { get; }
            new IDictionary<int,MCString> MCStringDic { get; }
            new IDictionary<int,CodeString> CodeStringDic { get; }
        }

        [Test]
        public void IDictionary_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required_for_basic_ref_types()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( IAbstractBasicRefDic ), typeof( IBasicRefDics ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            var d = auto.Services.GetRequiredService<PocoDirectory>();
            var pBase = d.Create<IBasicRefDics>();
            pBase.StringDic.Should().NotBeNull();
            pBase.ExtendedCultureInfoDic.Should().NotBeNull();
            pBase.NormalizedCultureInfoDic.Should().NotBeNull();
            pBase.MCStringDic.Should().NotBeNull();
            pBase.CodeStringDic.Should().NotBeNull();
            pBase.NormalizedCultureInfoDic.Should().BeAssignableTo<IReadOnlyDictionary<int,ExtendedCultureInfo>>();
        }
    }
}
