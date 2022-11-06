using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    /// <summary>
    /// C#8 introduced Default Implementation Methods (DIM). It MUST be an "AutoImplementationClaim":
    /// they don't appear as Poco properties. This perfectly fits the DIM design: they can be called only
    /// through the interface, not from the implementing class nor from other interfaces.
    ///
    /// One could be tempted here to support some automatic (intelligent?) support for this like for
    /// instance a [SharedImplementation] attributes that will make the DIM visible (and relayed to the DIM
    /// implementation) from the other Poco interfaces that have the property name (or a [RelayImplementation]
    /// that "imports" the property implementation from another interface).
    /// This is not really difficult and de facto implement a multiple inheritance capability...
    ///
    /// However, I'm a bit reluctant to do this since this would transform
    /// IPoco from a DTO structure to an Object beast. Such IPoco become far less "exchangeable" with the external
    /// world since they would lost their behavior. The funny Paradox here is that this would not be a real issue
    /// with "real" Methods that do things: nobody will be surprised to have "lost" these methods in Type Script,
    /// but for DIM properties (typically computed values) this will definitely be surprising. In practice, the
    /// code would often has to be transfered "on the other side", with the data...
    ///
    /// Choosing here to NOT play the multiple inheritance game is clearly the best choice (at least for me :)).
    /// 
    /// </summary>
    [TestFixture]
    public class DefaultImplementationMethodsTests
    {
        [CKTypeDefiner]
        public interface IRootDefiner : IPoco
        {
            IList<string> Lines { get; }

            [AutoImplementationClaim]
            int LineCount => Lines.Count;
        }

        public interface IActualRoot : IRootDefiner
        {
            IList<string> Rows { get; }

            [AutoImplementationClaim]
            int RowCount
            {
                get => Rows.Count;
                set
                {
                    while( Rows.Count > value ) Rows.RemoveAt( Rows.Count - 1 );
                }
            }

            [AutoImplementationClaim]
            void Clear()
            {
                Lines.Clear();
                Rows.Clear();
            }
        }

        [Test]
        public void Default_Implementation_Methods_are_supported()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IActualRoot ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var d = s.GetRequiredService<PocoDirectory>();
            var fA = d.Find( "CK.StObj.Engine.Tests.Poco.DefaultImplementationMethodsTests.IActualRoot" );
            Debug.Assert( fA != null );
            var magic = (IActualRoot)fA.Create();

            magic.LineCount.Should().Be( 0 );
            magic.Lines.Add( "Crazy" );
            magic.LineCount.Should().Be( 1 );
            magic.Lines.Add( "Isn't it?" );
            magic.LineCount.Should().Be( 2 );

            magic.RowCount.Should().Be( 0 );
            magic.Rows.Add( "Dingue" );
            magic.RowCount.Should().Be( 1 );
            magic.Rows.Add( "N'est-il pas ?" );
            magic.RowCount.Should().Be( 2 );

            magic.Clear();
            magic.Lines.Should().BeEmpty();
            magic.Rows.Should().BeEmpty();
            magic.LineCount.Should().Be( 0 );
            magic.RowCount.Should().Be( 0 );
        }

        public interface IOnActual : IActualRoot
        {
            // ERROR here! Regular property but RowCount is a DIM on IActualRoot.
            new int RowCount { get; set; }
        }

        [Test]
        public void homonym_properties_must_all_be_Default_Implementation_Method_or_not_in_a_Family1()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IActualRoot ), typeof( IOnActual ) );
            TestHelper.GetFailedResult( c, "has a Default Implementation Method (DIM). To be supported, all 'RowCount' properties must be DIM and use the [AutoImplementationClaim] attribute." );
        }

        public interface IFaultyRoot : IPoco
        {
            // ERROR here! 
            int X => 0;
        }

        [Test]
        public void a_Default_Implementation_Method_must_use_AutoImplementationClaim_Attribute()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IFaultyRoot ) );
            TestHelper.GetFailedResult( c, "is a Default Implemented Method (DIM), it must use the [AutoImplementationClaim] attribute." );
        }

        public interface IEmptyRoot : IPoco { }

        public interface IOther : IEmptyRoot
        {
            [AutoImplementationClaim]
            int ValidDIM => 1 + Random.Shared.Next( 50 );
        }

        public interface IAnother : IEmptyRoot
        {
            // ERROR here! IOther defines ValidDIM as a DIM.
            int ValidDIM { get; }
        }

        [Test]
        public void homonym_properties_must_all_be_Default_Implementation_Method_or_not_in_a_Family2()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IEmptyRoot ), typeof( IOther ), typeof( IAnother ) );
            TestHelper.GetFailedResult( c, "has a Default Implementation Method (DIM). To be supported, all 'ValidDIM' properties must be DIM and use the [AutoImplementationClaim] attribute." );
        }


    }
}
