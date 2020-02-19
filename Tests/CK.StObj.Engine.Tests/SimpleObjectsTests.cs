using System;
using CK.Core;
using CK.Setup;
using CK.StObj.Engine.Tests.SimpleObjects;
using NUnit.Framework;
using System.Linq;
using FluentAssertions;

using static CK.Testing.StObjEngineTestHelper;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public class SimpleObjectsTests
    {
        static Assembly ThisAssembly = typeof( SimpleObjectsTests ).Assembly;

        public class ObjectALevel1Conflict : ObjectA
        {
        }

        [Test]
        public void StObj_must_have_only_one_specialization_chain()
        {
            var types = ThisAssembly.GetTypes()
                            .Where( t => t.IsClass )
                            .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects" );

            var collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            collector.RegisterType( typeof( ObjectALevel1Conflict ) );
            var result = collector.GetResult();
            result.HasFatalError.Should().BeTrue();
        }

        public class Auto : IAutoService { }

        public class RealThatReferenceAutoIsError : IRealObject
        {
            void StObjConstruct( Auto s ) { }
        }

        public class AmbientInject : IRealObject
        {
            [InjectObject]
            public Auto Service { get; private set; }
        }

        [Test]
        public void a_RealObject_that_references_an_auto_service_from_its_StObjConstruct_is_an_error()
        {
            var types = new[] { typeof( RealThatReferenceAutoIsError ) };
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            var result = collector.GetResult();
            Assert.That( result.HasFatalError, Is.True );
        }

        [Test]
        public void a_RealObject_that_references_an_auto_service_from_an_InjectObject_is_an_error()
        {
            var types = new[] { typeof( AmbientInject ) };
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            var result = collector.GetResult();
            Assert.That( result.HasFatalError, Is.True );
        }

        [Test]
        public void Discovering_SimpleObjects()
        {
            var types = ThisAssembly.GetTypes()
                            .Where( t => t.IsClass )
                            .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects" );

            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            
            var result = TestHelper.GetSuccessfulResult( collector );

            IStObjResult oa = result.StObjs.ToStObj( typeof(ObjectA) );
            oa.Container.ObjectType.Should().Be( typeof( PackageForAB ) );
            oa.LeafSpecialization.ObjectType.Should().Be( typeof( ObjectALevel3 ) );

            IStObjResult oa1 = result.StObjs.ToStObj( typeof( ObjectALevel1 ) );
            oa1.Generalization.Should().BeSameAs( oa );
            oa1.Container.ObjectType.Should().Be( typeof( PackageForABLevel1 ) );

            IStObjResult oa2 = result.StObjs.ToStObj( typeof( ObjectALevel2 ) );
            oa2.Generalization.Should().BeSameAs( oa1 );
            oa2.Container.ObjectType.Should().Be( typeof( PackageForABLevel1 ), "Inherited." );

            IStObjResult oa3 = result.StObjs.ToStObj( typeof( ObjectALevel3 ) );
            oa3.Generalization.Should().BeSameAs( oa2 );
            oa3.Container.ObjectType.Should().Be( typeof( PackageForABLevel1 ), "Inherited." );
            oa.RootGeneralization.ObjectType.Should().Be( typeof( ObjectA ) );
        }

        [Test]
        public void Discovering_with_Level3()
        {
            using( TestHelper.Monitor.OpenInfo( "Without ObjectALevel4 class." ) )
            {
                var types = ThisAssembly.GetTypes()
                                .Where( t => t.IsClass )
                                .Where( t => (t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects"
                                              || t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects.WithLevel3")
                                             && t.Name != "ObjectALevel4" );

                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterTypes( types.ToList() );

                var result = collector.GetResult( );
                Assert.That( result.HasFatalError, Is.False );
            }

            using( TestHelper.Monitor.OpenInfo( "ObjectALevel4 class (specializes ObjectALevel3 and use IAbstractionBOnLevel2)." ) )
            {
                var types = ThisAssembly.GetTypes()
                                .Where( t => t.IsClass )
                                .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects"
                                             || t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects.WithLevel3" );

                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterTypes( types.ToList() );

                var result = collector.GetResult();
                Assert.That( result.HasFatalError, Is.False );
            }
        }

        [Test]
        public void Cycle_in_package()
        {
            using( TestHelper.Monitor.OpenInfo( "A specialization of ObjectBLevel3 wants to be in PackageForAB." ) )
            {
                // ↳ PackageForAB ∋ ObjectBLevel3_InPackageForAB ⇒ ObjectBLevel2 ⇒ ObjectBLevel1 ∈ PackageForABLevel1 ⇒ PackageForAB.
                var types = ThisAssembly.GetTypes()
                                .Where( t => t.IsClass )
                                .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects"
                                             || t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects.WithLevel3"
                                             || t.Name == "ObjectBLevel3_InPackageForAB" );

                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterTypes( types.ToList() );

                var result = collector.GetResult(  );
                Assert.That( result.HasFatalError, Is.True );
            }
        }

        public class ObjectXNeedsY : IRealObject
        {
            void StObjConstruct( ObjectYNeedsX other )
            {
                // This ObjectXNeedsY along with ObjectYNeedsX is used in two scenarii:
                // - They create a cycle: this was tested by the following.
                //   Assert.Fail( "Cycle: no object graph initialization." );
                // - It is also tested without the ObjectYNeedsX missing in registration.
                //   In such case, there is NO cycle, but the missing reference is
                //   detected when the graph is Constructed and a default value (null) is
                //   injected in order for other errors to be detected.
                //   ==> SimpleObjectsTests.MissingReference will success (result.HasFatal is true)
                //       but if we Assert.Fail here, NUnit 3.10.1 consider the test to have failed.
                //       This is why we HARD FAIL only if this construct is called while the ObjectYNeedsX
                //       is available.
                if( other != null ) Assert.Fail( "Cycle: no object graph initialization." );
            }
        }

        public class ObjectYNeedsX : IRealObject
        {
            void StObjConstruct( ObjectXNeedsY other )
            {
                // See comments in ObjectXNeedsY constructor.
                Assert.Fail( "Cycle: no object graph initialization." );
            }

        }

        [Test]
        public void ObjectXNeedsY_and_ObjectYNeedsX_Cycle()
        {
            using( TestHelper.Monitor.OpenInfo( "ObjectXNeedsY and ObjectYNeedsX." ) )
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( ObjectXNeedsY ) );
                collector.RegisterType( typeof( ObjectYNeedsX ) );

                TestHelper.GetFailedResult( collector );
            }
        }

        [Test]
        public void Missing_reference()
        {
            using( TestHelper.Monitor.OpenInfo( "ObjectXNeedsY without ObjectYNeedsX." ) )
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( ObjectXNeedsY ) );
                TestHelper.GetFailedResult( collector );
            }
        }

        [Test]
        public void IActivityMonitor_injected_in_the_StObjConstruct_is_the_Setup_monitor()
        {
            using( TestHelper.Monitor.OpenInfo( "ConsoleMonitor injection (and optional parameter)." ) )
            {
                var types = new[] { typeof( SimpleObjects.LoggerInjection.LoggerInjected ) };

                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterTypes( types.ToList() );
                var result = collector.GetResult(  );
                Assert.That( result.HasFatalError, Is.False );

                IStObjResult theObject = result.StObjs.ToLeaf( typeof(CK.StObj.Engine.Tests.SimpleObjects.LoggerInjection.LoggerInjected) );
                Assert.That( theObject, Is.Not.Null );
                Assert.That( theObject.InitialObject, Is.Not.Null.And.InstanceOf<CK.StObj.Engine.Tests.SimpleObjects.LoggerInjection.LoggerInjected>() );
            }
        }


        public class Dep0 : IRealObject { }

        public class Dep1 : IRealObject { }

        public class Dep2 : IRealObject { }


        public class BaseClass
        {
            public int StObjInitializeCallCount;
            public int RegisterStartupServicesCallCount;
            public int ConfigureServicesCallCount;

            void StObjConstruct( Dep0 d )
            {
                Dep0 = d;
            }

            void StObjInitialize( IActivityMonitor m, IStObjObjectMap map ) => ++StObjInitializeCallCount;

            void RegisterStartupServices( IActivityMonitor m, SimpleServiceContainer startupServices ) => ++RegisterStartupServicesCallCount;

            void ConfigureServices( in StObjContextRoot.ServiceRegister register ) => ++ConfigureServicesCallCount;

            public Dep0 Dep0 { get; private set; }
        }


        [CKTypeSuperDefiner]
        public class SuperDef : BaseClass, IRealObject
        {
            void StObjConstruct( Dep1 d )
            {
                Dep1 = d;
            }

            void StObjInitialize( IActivityMonitor m, IStObjObjectMap map ) => ++StObjInitializeCallCount;

            void RegisterStartupServices( IActivityMonitor m, SimpleServiceContainer startupServices ) => ++RegisterStartupServicesCallCount;

            void ConfigureServices( in StObjContextRoot.ServiceRegister register ) => ++ConfigureServicesCallCount;

            public Dep1 Dep1 { get; private set; }
        }

        public class Def : SuperDef
        {
            void StObjConstruct( Dep2 d )
            {
                Dep2 = d;
            }

            void StObjInitialize( IActivityMonitor m, IStObjObjectMap map ) => ++StObjInitializeCallCount;

            void RegisterStartupServices( IActivityMonitor m, SimpleServiceContainer startupServices ) => ++RegisterStartupServicesCallCount;

            void ConfigureServices( in StObjContextRoot.ServiceRegister register ) => ++ConfigureServicesCallCount;

            public Dep2 Dep2 { get; private set; }
        }

        public class Defined : Def { }

        [Test]
        public void StObjConstruct_StObjInitialize_RegisterStartupServices_and_ConfigureServices_of_the_hierarchy_are_called()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( Dep0 ) );
            collector.RegisterType( typeof( Dep1 ) );
            collector.RegisterType( typeof( Dep2 ) );
            collector.RegisterType( typeof( Defined ) );
            var r = TestHelper.GetAutomaticServices( collector );
            r.Services.GetService<SuperDef>().Should().BeNull( "This is a SuperDefiner. It is NOT a real object." );
            r.Services.GetService<Def>().Should().BeNull( "This is SuperDefiner direct specialization. It is NOT a real object." );
            var defined = r.Services.GetRequiredService<Defined>();
            defined.Dep0.Should().NotBeNull();
            defined.Dep1.Should().NotBeNull();
            defined.Dep2.Should().NotBeNull();
            defined.StObjInitializeCallCount.Should().Be( 3 );
            defined.RegisterStartupServicesCallCount.Should().Be( 3 );
            defined.ConfigureServicesCallCount.Should().Be( 3 );
        }


        #region Buggy & Valid Model

        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class C1 : IRealObject
        {
        }

        [StObj( Container = typeof( C1 ), ItemKind = DependentItemKindSpec.Container )]
        public class C2InC1 : IRealObject
        {
        }

        public class C3InC2SpecializeC1 : C1
        {
            void StObjConstruct( [Container]C2InC1 c2 )
            {
            }
        }

        [Test]
        public void BuggyModelBecauseOfContainment()
        {
            //Error: Cycle detected: 
            //    ↳ []CK.StObj.Engine.Tests.SimpleObjectsTests+C1 
            //        ⊐ []CK.StObj.Engine.Tests.SimpleObjectsTests+C2InC1 
            //            ⊐ []CK.StObj.Engine.Tests.SimpleObjectsTests+C3InC2SpecializeC1 
            //                ↟ []CK.StObj.Engine.Tests.SimpleObjectsTests+C1.

            var types = ThisAssembly.GetTypes()
                            .Where( t => t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C1"
                                         || t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C2InC1"
                                         || t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C3InC2SpecializeC1" );


            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            var result = collector.GetResult( );
            Assert.That( result.HasFatalError, Is.True );
        }

        [StObj( ItemKind = DependentItemKindSpec.Container, Container = typeof( C2InC1 ), Children = new Type[] { typeof( C1 ) } )]
        public class C3ContainsC1 : IRealObject
        {
        }

        [Test]
        public void BuggyModelBecauseOfContainmentCycle()
        {
            //Error: Cycle detected: 
            //    ↳ []CK.StObj.Engine.Tests.SimpleObjectsTests+C1 
            //        ⊏ []CK.StObj.Engine.Tests.SimpleObjectsTests+C3ContainsC1 
            //            ⊏ []CK.StObj.Engine.Tests.SimpleObjectsTests+C2InC1 
            //                ⊏ []CK.StObj.Engine.Tests.SimpleObjectsTests+C1.

            var types = ThisAssembly.GetTypes()
                            .Where( t => t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C1"
                                         || t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C2InC1"
                                         || t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C3ContainsC1" );

            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            var result = collector.GetResult(  );
            Assert.That( result.HasFatalError, Is.True );
        }

        public class C3RequiresC2SpecializeC1 : C1
        {
            void StObjConstruct( C2InC1 c2 )
            {
            }
        }

        [Test]
        public void ValidModelWithRequires()
        {
            var types = ThisAssembly.GetTypes()
                           .Where( t => t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C1"
                                        || t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C2InC1"
                                        || t.FullName == "CK.StObj.Engine.Tests.SimpleObjectsTests+C3RequiresC2SpecializeC1" );

            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );
            var result = collector.GetResult(  );
            Assert.That( result.HasFatalError, Is.False );
        
        }
        #endregion

    }
}
