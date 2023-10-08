using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public class AlsoRegisterTypeAttributeTests
    {
        [AlsoRegisterType(typeof( INestedPoco ))]
        public class StartingPoint : IRealObject
        {
            public interface INestedPoco : IPoco
            {
            }

            public interface IOtherNestedPoco : IPoco
            {
            }

            [AlsoRegisterType( typeof( IOtherNestedPoco ) )]
            [AlsoRegisterType( typeof( ARealObject ) )]
            public void JustForFun()
            {
            }
        }

        public class ARealObject : IRealObject
        {
        }

        [Test]
        public void AlsoRegisterTypeAttribute_works_recusively()
        {
            var c = TestHelper.CreateStObjCollector( typeof(StartingPoint) );
            var r = c.GetResult().CKTypeResult;
            Debug.Assert( r.PocoSupport != null );
            // The nested Poco is registered.
            r.PocoSupport.AllInterfaces[typeof( StartingPoint.INestedPoco )].Root.Name.Should().Be( "CK.StObj.Engine.Tests.AlsoRegisterTypeAttributeTests+StartingPoint+INestedPoco" );

            // The attribute can be on a method.
            r.PocoSupport.AllInterfaces[typeof( StartingPoint.IOtherNestedPoco )].Root.Name.Should().Be( "CK.StObj.Engine.Tests.AlsoRegisterTypeAttributeTests+StartingPoint+IOtherNestedPoco" );
            r.RealObjects.ConcreteClasses.SelectMany( c => c ).Should().ContainSingle( x => x.ClassType.Name == "ARealObject" );
        }
    }
}
