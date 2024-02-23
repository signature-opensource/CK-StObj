using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests
{
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
            IReadOnlySet<IAbstractBase> Set { get; }
        }

        public interface IPocoWithSetOfPrimary : IPoco, IWithSet
        {
            new ISet<IVerySimplePoco> Set { get; }
        }

        public interface IPocoWithSetOfSecondary : IPoco, IWithSet, IWithReadOnlySet
        {
            new ISet<ISecondaryVerySimplePoco> Set { get; }
        }

        public interface IPocoWithSetOfOtherSecondary : IPoco, IWithSet, IWithReadOnlySet
        {
            new ISet<IOtherSecondaryVerySimplePoco> Set { get; }
        }

        public interface IPocoWithSetOfAbstract : IPoco, IWithSet, IWithReadOnlySet
        {
            new ISet<IAbstract2> Set { get; }
        }

        [TestCase( typeof( IPocoWithSetOfPrimary ) )]
        [TestCase( typeof( IPocoWithSetOfSecondary ) )]
        [TestCase( typeof( IPocoWithSetOfOtherSecondary ) )]
        [TestCase( typeof( IPocoWithSetOfAbstract ) )]
        public void ISet_implementation_supports_all_the_required_types( Type type )
        {
            var requiredType = type == typeof( IPocoWithSetOfSecondary )
                                ? typeof( ISecondaryVerySimplePoco )
                                : type == typeof( IPocoWithSetOfOtherSecondary )
                                    ? typeof( IOtherSecondaryVerySimplePoco )
                                    : type; // duplicate registered type as a fallback.
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                                     typeof( IVerySimplePoco ), requiredType,
                                                     type );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var p = (IWithSet)d.Find( type )!.Create();

            p.Set.Should()
                .BeAssignableTo<IReadOnlySet<object>>()
                .And.BeAssignableTo<IReadOnlySet<IPoco>>();

            if( type != typeof( IPocoWithSetOfAbstract ) )
            {
                p.Set.Should().BeAssignableTo<ISet<IVerySimplePoco>>()
                    .And.BeAssignableTo<IReadOnlySet<IVerySimplePoco>>();

                if( type != typeof( IPocoWithSetOfPrimary ) )
                {
                    p.Set.Should().BeAssignableTo<IReadOnlySet<IAbstractBase>>();
                    if( type == typeof( IPocoWithSetOfSecondary ) )
                    {
                        p.Set.Should().BeAssignableTo<IReadOnlySet<IAbstract1>>()
                            .And.BeAssignableTo<ISet<ISecondaryVerySimplePoco>>()
                            .And.BeAssignableTo<IReadOnlySet<ISecondaryVerySimplePoco>>();

                        p.Set.Should().NotBeAssignableTo<IReadOnlySet<IAbstract2>>();
                    }
                    else
                    {
                        Throw.DebugAssert( type == typeof( IPocoWithSetOfOtherSecondary ) );
                        p.Set.Should().BeAssignableTo<IReadOnlySet<IAbstract2>>()
                            .And.BeAssignableTo<ISet<IOtherSecondaryVerySimplePoco>>()
                            .And.BeAssignableTo<IReadOnlySet<IOtherSecondaryVerySimplePoco>>();
                        p.Set.Should().NotBeAssignableTo<IReadOnlySet<IAbstract1>>();
                    }
                }
            }
        }

        public interface IPocoWithSetOfAbstractBase : IPoco
        {
            ISet<IAbstractBase> Set { get; }
        }

        public interface IPocoWithSetOfAbstract1 : IPoco
        {
            ISet<IAbstract1> Set { get; }
        }

        public interface IPocoWithSetOfClosed : IPoco
        {
            ISet<IClosed> Set { get; }
        }

        [Test]
        public void ISet_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                                     typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                                     typeof( IPocoWithSetOfAbstractBase ), typeof( IPocoWithSetOfAbstract1 ),
                                                     typeof( IAbstract1Closed ), typeof( IClosed ), typeof( IPocoWithSetOfClosed ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();

            var pBase = d.Create<IPocoWithSetOfAbstractBase>();
            pBase.Set.Should().BeAssignableTo<IReadOnlySet<object>>()
                .And.BeAssignableTo<IReadOnlySet<IPoco>>()
                .And.BeAssignableTo<IReadOnlySet<IAbstractBase>>();

            var pAbstract1 = d.Create<IPocoWithSetOfAbstract1>();
            pAbstract1.Set.Should().BeAssignableTo<IReadOnlySet<object>>()
                .And.BeAssignableTo<IReadOnlySet<IPoco>>()
                .And.BeAssignableTo<IReadOnlySet<IAbstractBase>>()
                .And.BeAssignableTo<IReadOnlySet<IAbstract1>>();

            var pClosed = d.Create<IPocoWithSetOfClosed>();
            pClosed.Set.Should().BeAssignableTo<IReadOnlySet<object>>()
                .And.BeAssignableTo<IReadOnlySet<IPoco>>()
                .And.BeAssignableTo<IReadOnlySet<IAbstractBase>>()
                .And.BeAssignableTo<IReadOnlySet<IAbstract1>>()
                .And.BeAssignableTo<IReadOnlySet<IAbstract1Closed>>()
                .And.BeAssignableTo<IReadOnlySet<IClosedPoco>>();
        }

        [CKTypeDefiner]
        public interface IAbstractBasicRefSets : IPoco
        {
            IReadOnlySet<object> StringSet { get; }
            IReadOnlySet<object> ExtendedCultureInfoSet { get; }
            IReadOnlySet<object> NormalizedCultureInfoSet { get; }
            IReadOnlySet<object> MCStringSet { get; }
            IReadOnlySet<object> CodeStringSet { get; }
        }

        public interface IBasicRefSets : IAbstractBasicRefSets
        {
            new ISet<string> StringSet { get; }
            new ISet<ExtendedCultureInfo> ExtendedCultureInfoSet { get; }
            new ISet<NormalizedCultureInfo> NormalizedCultureInfoSet { get; }
            new ISet<MCString> MCStringSet { get; }
            new ISet<CodeString> CodeStringSet { get; }
        }

        [Test]
        public void ISet_implementation_of_Abstract_is_NOT_natively_covariant_an_adpater_is_required_for_basic_ref_types()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBasicRefSets ), typeof( IBasicRefSets ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var pBase = d.Create<IBasicRefSets>();
            pBase.StringSet.Should().NotBeNull();
            pBase.ExtendedCultureInfoSet.Should().NotBeNull();
            pBase.NormalizedCultureInfoSet.Should().NotBeNull();
            pBase.MCStringSet.Should().NotBeNull();
            pBase.CodeStringSet.Should().NotBeNull();
            pBase.NormalizedCultureInfoSet.Should().BeAssignableTo<IReadOnlySet<ExtendedCultureInfo>>();
        }
    }
}
