using CK.Core;
using CK.Setup;
using Shouldly;
using NUnit.Framework;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class AlsoRegisterTypeAttributeTests
{
    [AlsoRegisterType( typeof( INestedPoco ) )]
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
        var c = new StObjCollector();
        c.RegisterType( TestHelper.Monitor, typeof( StartingPoint ) );
        var r = c.GetResult( TestHelper.Monitor );
        Throw.DebugAssert( !r.HasFatalError );
        // The nested Poco is registered.
        r.PocoTypeSystemBuilder.PocoDirectory.AllInterfaces[typeof( StartingPoint.INestedPoco )].PocoInterface.FullName.ShouldBe( "CK.StObj.Engine.Tests.AlsoRegisterTypeAttributeTests+StartingPoint+INestedPoco" );

        // The attribute can be on a method.
        r.PocoTypeSystemBuilder.PocoDirectory.AllInterfaces[typeof( StartingPoint.IOtherNestedPoco )].PocoInterface.FullName.ShouldBe( "CK.StObj.Engine.Tests.AlsoRegisterTypeAttributeTests+StartingPoint+IOtherNestedPoco" );
        r.CKTypeResult.RealObjects.ConcreteClasses.SelectMany( c => c ).Where( x => x.ClassType.Name == "ARealObject" ).ShouldHaveSingleItem();
    }
}
