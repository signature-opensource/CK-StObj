using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class RecursivePocoTests
    {
        public interface IDirectError : IPoco
        {
            IDirectError Pouf { get; }
        }

        [Test]
        public void Direct_recursion_is_detected()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IDirectError ) );
            TestHelper.GetFailedResult( c, "Detected an instantiation cycle in Poco:" );
        }

        public interface ICycleError : IPoco
        {
            ICycleError1 Pouf1 { get; }
        }

        public interface ICycleError1 : IPoco
        {
            ICycleError Pouf { get; }
        }

        [Test]
        public void Indirect_one_level_recursion_is_detected()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICycleError ), typeof( ICycleError1 ) );
            TestHelper.GetFailedResult( c, "Detected an instantiation cycle in Poco:" );
        }

        public interface ICycleErrorA : IPoco
        {
            ICycleErrorB PoufB { get; }
        }

        public interface ICycleErrorB : IPoco
        {
            ICycleErrorC PoufC { get; }
        }

        public interface ICycleErrorC : IPoco
        {
            ICycleErrorD PoufD { get; }
        }

        public interface ICycleErrorD : IPoco
        {
            ICycleErrorA PoufA { get; }
        }

        [Test]
        public void Indirect_multiple_level_recursion_is_detected()
        {
            var c = TestHelper.CreateStObjCollector( typeof( ICycleErrorA ), typeof( ICycleErrorB ), typeof( ICycleErrorC ), typeof( ICycleErrorD ) );
            TestHelper.GetFailedResult( c, "Detected an instantiation cycle in Poco:" );
        }

        public interface ICycleErrorConsumerIntermediate : IPoco
        {
            ICycleErrorB C { get; }
        }

        public interface ICycleErrorConsumer : IPoco
        {
            ICycleErrorConsumerIntermediate C { get; }
        }

        [Test]
        public void cycle_detection_is_independent_of_registration_order()
        {
            var types = new[] { typeof( ICycleErrorConsumer ),
                                typeof( ICycleErrorConsumerIntermediate ),
                                typeof( ICycleErrorA ),
                                typeof( ICycleErrorB ),
                                typeof( ICycleErrorC ),
                                typeof( ICycleErrorD ) };
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( types ), "Detected an instantiation cycle in Poco:" );
            Array.Reverse( types );
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( types ), "Detected an instantiation cycle in Poco:" );
        }

        public interface IHolder : IPoco
        {
            ref (int A, ((IOther IAmHere, int B) Inside, int C) DeepInside) Pof { get; }

        }

        public interface IOther : IPoco
        {
            ref (int A, (IHolder IAmHere, int B) Inside) Pof { get; }
        }

        [Test]
        public void recursion_through_record_fields_is_detected()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHolder ), typeof( IOther ) );
            TestHelper.GetFailedResult( c, "Detected an instantiation cycle in Poco: " );
        }

        public interface IHoldRecList : IPoco
        {
            public record struct Rec( IList<Rec> R, int A );

            ref Rec P { get; }
        }

        [Explicit]
        [Test]
        public void recursive_list_use_of_named_record_is_NOT_YET_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecList ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;
            var tRec = ts.FindByType( typeof( IHoldRecList.Rec ) ) as IRecordPocoType;
            Debug.Assert( tRec != null );
            var list = tRec.Fields[0].Type as ICollectionPocoType;
            Debug.Assert( list != null );
            list.ItemTypes[0].Should().Be( tRec );
        }

        public interface IHoldRecArray : IPoco
        {
            public record struct Rec( Rec[] R, int A );

            ref Rec P { get; }
        }

        [Explicit]
        [Test]
        public void recursive_array_use_of_named_record_is_NOT_YET_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecArray ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;
            var tRec = ts.FindByType( typeof( IHoldRecList.Rec ) ) as IRecordPocoType;
            Debug.Assert( tRec != null );
            var list = tRec.Fields[0].Type as ICollectionPocoType;
            Debug.Assert( list != null );
            list.ItemTypes[0].Should().Be( tRec );
        }

        public interface IHoldRecDic : IPoco
        {
            public record struct Rec( Dictionary<int,Rec> R, int A );

            ref Rec P { get; }
        }

        [Explicit]
        [Test]
        public void recursive_dicionary_use_of_named_record_is_NOT_YET_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IHoldRecDic ) );
            var ts = TestHelper.GetSuccessfulResult( c ).CKTypeResult.PocoTypeSystem;
            var tRec = ts.FindByType( typeof( IHoldRecList.Rec ) ) as IRecordPocoType;
            Debug.Assert( tRec != null );
            var list = tRec.Fields[0].Type as ICollectionPocoType;
            Debug.Assert( list != null );
            list.ItemTypes[0].Should().Be( tRec );
        }


    }
}
