using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public class DifferentKindOfProperties
    {

        public class ObjA : IRealObject
        {
            [AmbientProperty]
            public ObjB NoProblem { get; set; }
        }

        public class ObjB : IRealObject
        {
            [StObjProperty]
            [AmbientProperty]
            public ObjA TwoAttributes { get; set; } 
        }

        public class ObjSpecA : ObjA
        {
            [StObjProperty]
            public new ObjB NoProblem { get; set; }
        }

        [StObjProperty( PropertyName = "NoProblem", PropertyType = typeof(object) )]
        public class ObjSpecA2 : ObjA
        {
        }

        [Test]
        public void StObjAndAmbientPropertiesAreIncompatible()
        {
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( ObjB ) );
                collector.RegisterType( typeof( ObjA ) );
                Assert.That( collector.RegisteringFatalOrErrorCount == 1 );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( ObjA ) );
                collector.RegisterType( typeof( ObjSpecA ) );
                Assert.That( collector.RegisteringFatalOrErrorCount == 1 );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( ObjA ) );
                collector.RegisterType( typeof( ObjSpecA2 ) );
                Assert.That( collector.RegisteringFatalOrErrorCount == 1 );
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

        [StObjProperty( PropertyName = "Albert", PropertyType = typeof(object) )]
        public class DuplicateStObjProperty : IRealObject
        {
            [StObjProperty]
            public object Albert { get; set; }
        }

        [Test]
        public void InvalidStObjProperties()
        {
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( MissingStObjPropertyType ) );
                Assert.That( collector.RegisteringFatalOrErrorCount == 1 );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( MissingStObjPropertyName ) );
                Assert.That( collector.RegisteringFatalOrErrorCount == 1 );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( DuplicateStObjProperty ) );
                Assert.That( collector.RegisteringFatalOrErrorCount == 1 );
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
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( InvalidRealObjectProperty ) );
                collector.GetResult().HasFatalError.Should().BeTrue();
            }
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
            public CA A { get; set; }
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
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( CB3 ) );
                collector.RegisterType( typeof( CA3 ) );
                var r = collector.GetResult(  );
                Assert.That( r.HasFatalError, Is.False );
                var cb = r.StObjs.Obtain<CB>();
                Assert.That( cb, Is.InstanceOf<CB3>() );
                Assert.That( cb.A, Is.InstanceOf<CA3>() );
            }
        }

        public class CMissingSetterOnTopDefiner : IRealObject
        {
            [InjectObject]
            public CA2 A { get { return null; } }
        }

        [Test]
        public void SetterMustExistOnTopDefiner()
        {
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( CMissingSetterOnTopDefiner ) );
                collector.RegisterType( typeof( CA2 ) );
                Assert.That( collector.RegisteringFatalOrErrorCount, Is.EqualTo( 1 ) );
            }
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
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( CPrivateSetter ) );
                collector.RegisterType( typeof( CA2 ) );
                var r = collector.GetResult( );
                Assert.That( r.HasFatalError, Is.False );
                var c = r.StObjs.Obtain<CPrivateSetter>();
                Assert.That( c.A, Is.InstanceOf<CA2>() );
            }
        }


        #endregion

    }
}
