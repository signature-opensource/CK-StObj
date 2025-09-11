using CK.Core;
using CK.Engine.TypeCollector;
using CK.Setup;
using NUnit.Framework;
using Shouldly;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class AlsoRegisterTypeAttributeTests
{
    [AlsoRegisterType<INestedPoco>]
    public class StartingPoint : IRealObject
    {
        [AlsoRegisterType<ARealObject, AnEnum>]
        public interface INestedPoco : IPoco
        {
        }

        public interface IOtherNestedPoco : IPoco
        {
        }

    }

    public class ARealObject : IRealObject
    {
    }

    [AlsoRegisterType<StartingPoint.IOtherNestedPoco>]
    public enum AnEnum
    {
    }

    [Test]
    public void AlsoRegisterTypeAttribute_works_recursively()
    {
        var configuration = new EngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( StartingPoint ) );

        var binPathResults = BinPathTypeGroup.Run( TestHelper.Monitor, configuration ).ShouldNotBeNull();
        binPathResults.Success.ShouldBeTrue();
        binPathResults.Groups.ShouldHaveSingleItem().ConfiguredTypes.AllTypes.Select( cT => cT.Type )
            .ShouldBe( [
                typeof(StartingPoint),
                typeof(StartingPoint.INestedPoco),
                typeof(ARealObject),
                typeof(AnEnum),
                typeof(StartingPoint.IOtherNestedPoco)
                ], ignoreOrder: true );
    }
}
