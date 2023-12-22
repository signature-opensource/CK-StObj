using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static CK.StObj.Engine.Tests.Poco.AbstractImplTests.MultiListImplementationTests;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests
{
    public class MultiSetImplementationTests : CommonTypes
    {
        [CKTypeDefiner]
        public interface IWithSet : IPoco
        {
            object Set { get; }
            //
            // This would be far better when this could be used:
            //
            // IReadOnlySet<IAbstractBase> Set { get; }
        }

        public interface IPocoWithSetOfPrimary : IPoco, IWithSet
        {
            new ISet<IVerySimplePoco> Set { get; }
        }

        public interface IPocoWithSetOfSecondary : IPoco, IWithSet
        {
            new ISet<ISecondaryVerySimplePoco> Set { get; }
        }

        public interface IPocoWithSetOfOtherSecondary : IPoco, IWithSet
        {
            new ISet<IOtherSecondaryVerySimplePoco> Set { get; }
        }

        [TestCase( typeof( IPocoWithSetOfPrimary ) )]
        [TestCase( typeof( IPocoWithSetOfSecondary ) )]
        [TestCase( typeof( IPocoWithSetOfOtherSecondary ) )]
        public void ISet_implementation_supports_all_the_required_types( Type type )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                                     typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                                     type );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var p = (IWithSet)d.Find( type )!.Create();

            p.Set.Should().BeAssignableTo<ISet<IVerySimplePoco>>()
                .And.BeAssignableTo<ISet<ISecondaryVerySimplePoco>>()
                .And.BeAssignableTo<ISet<IOtherSecondaryVerySimplePoco>>()
                .And.BeAssignableTo<IReadOnlySet<object>>()
                .And.BeAssignableTo<IReadOnlySet<IPoco>>()
                .And.BeAssignableTo<IReadOnlySet<IVerySimplePoco>>()
                .And.BeAssignableTo<IReadOnlySet<ISecondaryVerySimplePoco>>()
                .And.BeAssignableTo<IReadOnlySet<IOtherSecondaryVerySimplePoco>>();

            if( type != typeof( IPocoWithSetOfPrimary ) )
            {
                p.Set.Should().BeAssignableTo<IReadOnlySet<IAbstractBase>>();
                if( type == typeof( IPocoWithSetOfSecondary ) )
                {
                    p.Set.Should().BeAssignableTo<IReadOnlySet<IAbstract1>>();
                }
                else
                {
                    p.Set.Should().BeAssignableTo<IReadOnlySet<IAbstract2>>();
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
    }
}
