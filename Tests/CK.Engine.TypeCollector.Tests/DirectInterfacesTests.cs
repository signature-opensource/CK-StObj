using CK.Setup;
using Shouldly;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Engine.TypeCollector.Tests;

[TestFixture]
public class DirectInterfacesTests
{

    public interface I1 { }

    public class BaseObject : I1 { }

    public class SpecializedObject1 : BaseObject { }

    public class SpecializedObject2 : SpecializedObject1 { }

    [Test]
    public void from_BaseType()
    {
        {
            var config = new EngineConfiguration();
            config.FirstBinPath.Types.Add( typeof( SpecializedObject1 ) );
            var typeCache = BinPathTypeGroup.Run( TestHelper.Monitor, config ).ShouldNotBeNull().TypeCache;
            var o1 = typeCache.Find( typeof( SpecializedObject1 ) ).ShouldNotBeNull();
            var i1 = typeCache.Find( typeof( I1 ) ).ShouldNotBeNull();

            o1.Interfaces.ShouldContain( i1 );
            o1.DirectInterfaces.ShouldNotContain( i1 );
        }
        {
            var config = new EngineConfiguration();
            config.FirstBinPath.Types.Add( typeof( SpecializedObject2 ) );
            var typeCache = BinPathTypeGroup.Run( TestHelper.Monitor, config ).ShouldNotBeNull().TypeCache;


            var o2 = typeCache.Find( typeof( SpecializedObject2 ) ).ShouldNotBeNull();
            var i1 = typeCache.Find( typeof( I1 ) ).ShouldNotBeNull();

            o2.Interfaces.ShouldContain( i1 );
            o2.DirectInterfaces.ShouldNotContain( i1 );

            var o1 = typeCache.Find( typeof( SpecializedObject1 ) ).ShouldNotBeNull();
            o1.Interfaces.ShouldContain( i1 );
            o1.DirectInterfaces.ShouldNotContain( i1 );

        }
    }

    public interface J2 { }
    public interface I2 : I1 { }
    public interface I3 : I2, J2 { }
    public interface I4 : I3, I2, I1 { }
    public interface J4 : J2 { }
    public interface I5 : I4, J4 { }

    [Test]
    public void from_Interfaces()
    {
        var config = new EngineConfiguration();
        config.FirstBinPath.Types.Add( typeof( I5 ) );
        var typeCache = BinPathTypeGroup.Run( TestHelper.Monitor, config ).ShouldNotBeNull().TypeCache;

        var i1 = typeCache.Find( typeof( I1 ) ).ShouldNotBeNull();
        var j2 = typeCache.Find( typeof( J2 ) ).ShouldNotBeNull();
        var i2 = typeCache.Find( typeof( I2 ) ).ShouldNotBeNull();
        var i3 = typeCache.Find( typeof( I3 ) ).ShouldNotBeNull();
        var i4 = typeCache.Find( typeof( I4 ) ).ShouldNotBeNull();
        var j4 = typeCache.Find( typeof( J4 ) ).ShouldNotBeNull();
        var i5 = typeCache.Find( typeof( I5 ) ).ShouldNotBeNull();

        i5.Interfaces.ShouldBe( [i1, i2, j2, i3, i4, j4], ignoreOrder: true );
        i5.DirectInterfaces.ShouldBe( [i4, j4], ignoreOrder: true );

        i4.Interfaces.ShouldBe( [i1, i2, i3, j2], ignoreOrder: true );
        i4.DirectInterfaces.ShouldBe( [i3], ignoreOrder: true );
    }

}
