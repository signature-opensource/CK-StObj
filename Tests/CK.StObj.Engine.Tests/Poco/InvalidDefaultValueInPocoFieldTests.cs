using CK.Core;
using NUnit.Framework;
using System.Collections.Generic;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class InvalidDefaultValueInPocoFieldTests
    {
        public interface ISomePoco : IPoco
        {
        }

        public interface IInvalidObject1 : IPoco
        {
            ref (int Ok, object NoWay) P { get; }
        }

        public interface IInvalidObject2 : IPoco
        {
            ref (int Ok, (object NoWay, string Works) Sub) P { get; }
        }

        public record struct Thing( int Ok, (object NoWay, string Works) Sub );

        public interface IInvalidObject3 : IPoco
        {
            ref Thing P { get; }
        }

        public record struct Intermediate( int Ok, Thing Inner );

        public interface IInvalidObject4 : IPoco
        {
            ref (Intermediate Intermediate, int Power) P { get; }
        }

        public record struct JustForFun( int Ok, (Intermediate Intermediate, string Name) Another );

        public interface IInvalidObject5 : IPoco
        {
            ref JustForFun P { get; }
        }

        // Note that error is different for:
        //  - Any kind of Poco (abstract or not) and concrete collections (including arrays): Invalid mutable reference types in '...'
        //      ==> This is the "ReadOnlyCompliant rule" that applies.
        //  - Abstract collections: Invalid abstract collection 'IList<int>' in Property '...'. It must be a List.
        //      ==> This is the "Covariance can only be managed by the IPoco" restriction.
        [Test]
        public void non_nullable_object_is_invalid()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidObject1 ) );
                TestHelper.GetFailedResult( c, """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.IInvalidObject1', field: 'P.NoWay' has no default value.
                No default can be synthesized for non nullable '[Any]object'.
                """ );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidObject2 ) );
                TestHelper.GetFailedResult( c, """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.IInvalidObject2', field: 'P.Sub.NoWay' has no default value.
                No default can be synthesized for non nullable '[Any]object'.
                """ );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidObject3 ) );
                TestHelper.GetFailedResult( c, """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.IInvalidObject3', field: 'P' has no default value.
                Because '[Record]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.Thing', field: 'Sub.NoWay' has no default value.
                No default can be synthesized for non nullable '[Any]object'.
                """ );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidObject4 ) );
                TestHelper.GetFailedResult( c, """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.IInvalidObject4', field: 'P.Intermediate' has no default value.
                Because '[Record]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.Intermediate', field: 'Inner' has no default value.
                Because '[Record]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.Thing', field: 'Sub.NoWay' has no default value.
                No default can be synthesized for non nullable '[Any]object'.
                """ );
            }
            {
                var c = TestHelper.CreateStObjCollector( typeof( IInvalidObject5 ) );
                TestHelper.GetFailedResult( c, """
                Required computable default value is missing in Poco:
                '[PrimaryPoco]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.IInvalidObject5', field: 'P' has no default value.
                Because '[Record]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.JustForFun', field: 'Another.Intermediate' has no default value.
                Because '[Record]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.Intermediate', field: 'Inner' has no default value.
                Because '[Record]CK.StObj.Engine.Tests.Poco.InvalidDefaultValueInPocoFieldTests.Thing', field: 'Sub.NoWay' has no default value.
                No default can be synthesized for non nullable '[Any]object'.
                """ );
            }
        }


    }
}
