using CK.Core;
using CK.Testing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using static CK.Testing.MonitorTestHelper;

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
            TestHelper.GetFailedCollectorResult( [typeof( IDirectError )], "Detected an instantiation cycle in Poco:" );
        }

        public interface IDirectErrorWithSetter : IPoco
        {
            IDirectErrorWithSetter Pouf { get; set; }
        }

        [Test]
        public void Direct_recursion_is_detected_even_if_setter_is_used_because_of_initialization()
        {
            TestHelper.GetFailedCollectorResult( [typeof( IDirectErrorWithSetter )], "Detected an instantiation cycle in Poco:" );
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
            TestHelper.GetFailedCollectorResult( [typeof( ICycleError ), typeof( ICycleError1 )], "Detected an instantiation cycle in Poco:" );
        }

        public interface ICycleErrorWithSetter : IPoco
        {
            ICycleErrorWithSetter1 Pouf1 { get; set; }
        }

        public interface ICycleErrorWithSetter1 : IPoco
        {
            ICycleErrorWithSetter Pouf { get; set; }
        }

        [Test]
        public void Indirect_one_level_recursion_is_detected_even_with_setters()
        {
            TestHelper.GetFailedCollectorResult( [typeof( ICycleErrorWithSetter ), typeof( ICycleErrorWithSetter1 )], "Detected an instantiation cycle in Poco:" );
        }

        public interface ICommandOne : IPoco
        {
            string Name { get; set; }

            ICommandTwo Friend { get; }
        }

        public interface ICommandTwo : IPoco
        {
            int Age { get; set; }

            ICommandOne AnotherFriend { get; set; }

            ICommandThree FriendThree { get; set; }
        }

        public interface ICommandThree : IPoco
        {
            int Age { get; set; }
        }

        [Test]
        public void Indirect_one_level_recursion_is_detected_even_with_setters2()
        {
            TestHelper.GetFailedCollectorResult( [typeof( ICommandOne ), typeof( ICommandTwo ), typeof( ICommandThree )],
                """
                Detected an instantiation cycle in Poco: 
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.RecursivePocoTests.ICommandOne', field: 'Friend' => 
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.RecursivePocoTests.ICommandTwo', field: 'AnotherFriend' => '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.RecursivePocoTests.ICommandOne'.
                """ );
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
            TestHelper.GetFailedCollectorResult( [typeof( ICycleErrorA ), typeof( ICycleErrorB ), typeof( ICycleErrorC ), typeof( ICycleErrorD )], "Detected an instantiation cycle in Poco:" );
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
            TestHelper.GetFailedCollectorResult( types, "Detected an instantiation cycle in Poco:" );
            Array.Reverse( types );
            TestHelper.GetFailedCollectorResult( types, "Detected an instantiation cycle in Poco:" );
        }

        public interface IDirectErrorPrimary : IPoco
        {
        }

        public interface IDirectErrorSecondary : IDirectErrorPrimary
        {
            IDirectErrorSecondary Pouf { get; set; }
        }

        [Test]
        public void Direct_recursion_is_detected_through_secondary()
        {
            TestHelper.GetFailedCollectorResult( [typeof( IDirectErrorPrimary ), typeof( IDirectErrorSecondary )], "Detected an instantiation cycle in Poco:" );
        }

        #region Real life case that exhibited a stupid bug in initialization cycle detection (PocoCycleAndDefaultVisitor).
        public interface IPocoAuthenticationInfo : IPoco
        {
            IPocoUserInfo User { get; }
            IPocoUserInfo UnsafeUser { get; }
            IPocoUserInfo ActualUser { get; }
            bool IsImpersonated { get; set; }
            DateTime? Expires { get; set; }
            DateTime? CriticalExpires { get; set; }
        }

        public interface IPocoUserInfo : IPoco
        {
            string UserName { get; set; }
            int UserId { get; set; }
            IList<(string Scheme, DateTime LastUsed)> Schemes { get; }
        }

        [Test]
        public void multiple_fields_with_the_same_type_are_not_cycles()
        {
            TestHelper.GetSuccessfulCollectorResult( [typeof( IPocoAuthenticationInfo ), typeof( IPocoUserInfo )] );
        }
        #endregion


    }
}
