using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using FluentAssertions;

using static CK.Testing.StObjEngineTestHelper;
using System.Diagnostics;

namespace CK.StObj.Engine.Tests
{

    [TestFixture]
    public class AutoImplementationTests
    {

        [AttributeUsage( AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false )]
        public class AutoImplementMethodAttribute : Attribute, IAutoImplementationClaimAttribute
        {
        }

        public abstract class ABase
        {
            [AutoImplementMethod]
            protected abstract int FirstMethod( int i );
        }

        public abstract class A : ABase, IRealObject
        {
            [AutoImplementMethod]
            public abstract string SecondMethod( int i );
        }

        public abstract class A2 : A
        {
            //[AutoImplementMethod]
            public abstract A ThirdMethod( int i, string s );
        }


        [Test]
        public void abstract_auto_impl_is_supported_on_non_IRealObject_base_class()
        {
            StObjCollector collector = TestHelper.CreateStObjCollector( typeof( A2 ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            Debug.Assert( r.EngineMap != null, "No initialization error." );
            r.EngineMap.StObjs.Obtain<A>().Should().NotBeNull().And.BeAssignableTo<A2>();
        }

        public abstract class A2Spec : A2
        {
        }

        [Test]
        public void abstract_can_be_implemented_by_base_class()
        {
            StObjCollector collector = TestHelper.CreateStObjCollector( typeof( A2Spec ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            Debug.Assert( r.EngineMap != null, "No initialization error." );
            r.EngineMap.StObjs.Obtain<A>().Should().NotBeNull().And.BeAssignableTo<A2Spec>();
        }


        public abstract class A3 : A
        {
            public abstract A ThirdMethod( int i, string s );
        }

        [Test]
        public void abstract_non_auto_implementable_leaf_are_silently_ignored()
        {
            StObjCollector collector = TestHelper.CreateStObjCollector( typeof( A3 ) );
            var r = TestHelper.GetSuccessfulResult( collector );
            Debug.Assert( r.EngineMap != null, "No initialization error." );
            r.EngineMap.StObjs.Obtain<A>().Should().NotBeNull().And.BeAssignableTo<A>().And.NotBeAssignableTo<A3>();
        }

        [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
        class PreventAutoImplementationAttribute : Attribute { }

        [PreventAutoImplementation]
        public abstract class A4 : A
        {
            [AutoImplementMethod]
            public abstract A ThirdMethod( int i, string s );
        }


        [Test]
        public void abstract_auto_implementable_leaf_but_using_PreventAutoImplementationAttribute_are_silently_ignored()
        {
            StObjCollector collector = TestHelper.CreateStObjCollector( typeof( A4 ) );
            
            StObjCollectorResult result = TestHelper.GetSuccessfulResult( collector );
            Debug.Assert( result.EngineMap != null, "No initialization error." );

            result.EngineMap.StObjs.Obtain<A>().Should().NotBeNull().And.BeAssignableTo<A>().And.NotBeAssignableTo<A4>();
        }

    }
}
