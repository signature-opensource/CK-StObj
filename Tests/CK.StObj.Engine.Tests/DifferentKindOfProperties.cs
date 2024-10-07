using CK.Core;
using CK.Testing;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class DifferentKindOfProperties
{

    public class ObjA : IRealObject
    {
        [AmbientProperty]
        public ObjB? NoProblem { get; set; }
    }

    public class ObjB : IRealObject
    {
        [StObjProperty]
        [AmbientProperty]
        public ObjA? TwoAttributes { get; set; }
    }

    public class ObjSpecA : ObjA
    {
        [StObjProperty]
        public new ObjB? NoProblem { get; set; }
    }

    [StObjProperty( PropertyName = "NoProblem", PropertyType = typeof( object ) )]
    public class ObjSpecA2 : ObjA
    {
    }

    [Test]
    public void StObjAndAmbientPropertiesAreIncompatible()
    {
        {
            TestHelper.GetFailedCollectorResult( [typeof( ObjB ), typeof( ObjA )],
                "Property named 'TwoAttributes' for 'CK.StObj.Engine.Tests.DifferentKindOfProperties+ObjB' can not be both an Ambient Singleton, an Ambient Property or a StObj property." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( ObjA ), typeof( ObjSpecA )], "[StObjProperty] property 'CK.StObj.Engine.Tests.DifferentKindOfProperties+ObjSpecA.NoProblem' is declared as a '[AmbientProperty]' property by 'CK.StObj.Engine.Tests.DifferentKindOfProperties+ObjA'. Property names must be distinct." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( ObjA ), typeof( ObjSpecA2 )], "[StObjProperty] property 'CK.StObj.Engine.Tests.DifferentKindOfProperties+ObjSpecA2.NoProblem' is declared as a '[AmbientProperty]' property by 'CK.StObj.Engine.Tests.DifferentKindOfProperties+ObjA'. Property names must be distinct." );
        }
    }

    // A null property type triggers an error: it must be explicitly typeof(object).
    [StObjProperty( PropertyName = "AProperty", PropertyType = null )]
    public class MissingStObjPropertyType : IRealObject
    {
    }

    [StObjProperty( PropertyName = "  " )]
    public class MissingStObjPropertyName : IRealObject
    {
    }

    [StObjProperty( PropertyName = "Albert", PropertyType = typeof( object ) )]
    public class DuplicateStObjProperty : IRealObject
    {
        [StObjProperty]
        public object? Albert { get; set; }
    }

    [Test]
    public void InvalidStObjProperties()
    {
        {
            TestHelper.GetFailedCollectorResult( [typeof( MissingStObjPropertyType )], "StObj property named 'AProperty' for 'CK.StObj.Engine.Tests.DifferentKindOfProperties.MissingStObjPropertyType' has no PropertyType defined. It should be typeof(object) to explicitly express that any type is accepted." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( MissingStObjPropertyName )], "Unnamed or whitespace StObj property on 'CK.StObj.Engine.Tests.DifferentKindOfProperties.MissingStObjPropertyName'. Attribute must be configured with a valid PropertyName." );
        }
        {
            TestHelper.GetFailedCollectorResult( [typeof( DuplicateStObjProperty )], "StObj property named 'Albert' for 'CK.StObj.Engine.Tests.DifferentKindOfProperties.DuplicateStObjProperty' is defined more than once. It should be declared only once." );
        }
    }

    public class ScopedService : IScopedAutoService { }

    public class InvalidRealObjectProperty : IRealObject
    {
        [InjectObject]
        public ScopedService NotAnRealObjectPropertyType { get; protected set; }
    }

    [Test]
    public void InjectSingleton_must_not_be_scoped_service()
    {
        TestHelper.GetFailedCollectorResult( [typeof( InvalidRealObjectProperty )], "Inject Object 'NotAnRealObjectPropertyType' of 'CK.StObj.Engine.Tests.DifferentKindOfProperties+InvalidRealObjectProperty': CK.StObj.Engine.Tests.DifferentKindOfProperties+ScopedService not found." );
    }

    #region Covariance support

    public class CA : IRealObject
    {
    }

    public class CA2 : CA
    {
    }

    public class CA3 : CA2
    {
    }

    public class CB : IRealObject
    {
        [InjectObject]
        public CA A { get; private set; }
    }

    public class CB2 : CB
    {
        [InjectObject]
        public new CA2 A { get { return (CA2)base.A; } }
    }

    public class CB3 : CB2
    {
        [InjectObject]
        public new CA3 A
        {
            get { return (CA3)base.A; }
            set
            {
                Assert.Fail( "This is useless and is not called. This setter generates a warning." );
            }
        }
    }

    [Test]
    public void CovariantPropertiesSupport()
    {
        var map = TestHelper.GetSuccessfulCollectorResult( [typeof( CB3 ), typeof( CA3 )] ).EngineMap;
        Throw.DebugAssert( map != null );

        var cb = map.StObjs.Obtain<CB>()!;
        Assert.That( cb, Is.InstanceOf<CB3>() );
        Assert.That( cb.A, Is.InstanceOf<CA3>() );
    }

    public class CMissingSetterOnTopDefiner : IRealObject
    {
        [InjectObject]
        public CA2? A => null;
    }

    [Test]
    public void SetterMustExistOnTopDefiner()
    {
        TestHelper.GetFailedCollectorResult( [typeof( CMissingSetterOnTopDefiner ), typeof( CA2 )],
            "Property 'CK.StObj.Engine.Tests.DifferentKindOfProperties+CMissingSetterOnTopDefiner.A' must have a setter (since it is the first declaration of the property)." );
    }

    public class CPrivateSetter : IRealObject
    {
        [InjectObject]
        public CA2 A { get; private set; }
    }

    [Test]
    public void PrivateSetterWorks()
    {
        {
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( CPrivateSetter ), typeof( CA2 )] ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            var c = map.StObjs.Obtain<CPrivateSetter>()!;
            Assert.That( c.A, Is.InstanceOf<CA2>() );
        }
    }


    #endregion

}
