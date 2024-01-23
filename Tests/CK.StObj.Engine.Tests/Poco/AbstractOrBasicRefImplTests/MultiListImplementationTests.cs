using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco.AbstractImplTests
{
    public class MultiListImplementationTests : CommonTypes
    {
        [CKTypeDefiner]
        public interface IWithList : IPoco
        {
            object List { get; }
        }

        [CKTypeDefiner]
        public interface IWithReadOnlyList : IPoco
        {
            IReadOnlyList<IAbstractBase> List { get; }
        }

        public interface IPocoWithListOfPrimary : IPoco, IWithList
        {
            new IList<IVerySimplePoco> List { get; }
        }

        public interface IPocoWithListOfSecondary : IPoco, IWithList, IWithReadOnlyList
        {
            new IList<ISecondaryVerySimplePoco> List { get; }
        }

        public interface IPocoWithListOfOtherSecondary : IPoco, IWithList, IWithReadOnlyList
        {
            new IList<IOtherSecondaryVerySimplePoco> List { get; }
        }

        public interface IPocoWithListOfAbstract : IPoco, IWithList, IWithReadOnlyList
        {
            new IList<IAbstract2> List { get; }
        }

        [TestCase( typeof( IPocoWithListOfPrimary ) )]
        [TestCase( typeof( IPocoWithListOfSecondary ) )]
        [TestCase( typeof( IPocoWithListOfOtherSecondary ) )]
        [TestCase( typeof( IPocoWithListOfAbstract ) )]
        public void IList_implementation_supports_all_the_required_types( Type type )
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                                     typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                                     type );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var p = (IWithList)d.Find( type )!.Create();

            p.List.Should()
                .BeAssignableTo<IReadOnlyList<object>>()
                .And.BeAssignableTo<IReadOnlyList<IPoco>>();

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
        public void IList_implementation_of_Abstract_is_natively_covariant()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IAbstract2 ),
                                                     typeof( IVerySimplePoco ), typeof( ISecondaryVerySimplePoco ), typeof( IOtherSecondaryVerySimplePoco ),
                                                     typeof( IPocoWithListOfAbstractBase ), typeof( IPocoWithListOfAbstract1 ),
                                                     typeof( IAbstract1Closed ), typeof( IClosed ), typeof( IPocoWithListOfClosed ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();

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
            var c = TestHelper.CreateStObjCollector( typeof( IAbstractBase ), typeof( IAbstract1 ), typeof( IInvalid ), typeof( ISecondaryVerySimplePoco ) );
            TestHelper.GetFailedResult( c, "Property 'CK.StObj.Engine.Tests.Poco.AbstractImplTests.MultiListImplementationTests.IInvalid.List': Type must be 'IList<CK.StObj.Engine.Tests.Poco.AbstractImplTests.CommonTypes.IAbstractBase>' since 'CK.StObj.Engine.Tests.Poco.AbstractImplTests.MultiListImplementationTests.IPocoWithListOfAbstractBase.List' defines it." );
        }
    }
}
