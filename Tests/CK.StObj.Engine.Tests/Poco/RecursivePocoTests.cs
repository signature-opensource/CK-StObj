using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( typeof( IDirectError ) ) );
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
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( typeof( ICycleError ), typeof( ICycleError1 ) ) );
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
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( typeof( ICycleErrorA ), typeof( ICycleErrorB ), typeof( ICycleErrorC ), typeof( ICycleErrorD ) ) );
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
            var types = new[] { typeof( ICycleErrorConsumer ), typeof( ICycleErrorConsumerIntermediate ), typeof( ICycleErrorA ), typeof( ICycleErrorB ), typeof( ICycleErrorC ), typeof( ICycleErrorD ) };
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( types ) );
            Array.Reverse( types );
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( types ) );
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
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( typeof( IHolder ), typeof( IOther ) ) );
        }



    }
}
