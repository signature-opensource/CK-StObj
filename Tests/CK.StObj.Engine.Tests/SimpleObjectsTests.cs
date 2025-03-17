using CK.Core;
using CK.Setup;
using CK.StObj.Engine.Tests.SimpleObjects;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0051 // Remove unused private members

namespace CK.StObj.Engine.Tests;

[TestFixture]
public class SimpleObjectsTests
{
    static readonly Assembly ThisAssembly = typeof( SimpleObjectsTests ).Assembly;

    public class ObjectALevel1Conflict : ObjectA
    {
    }

    [Test]
    public void StObj_must_have_only_one_specialization_chain()
    {
        var types = ThisAssembly.GetTypes()
                        .Where( t => t.IsClass )
                        .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects" );

        TestHelper.GetFailedCollectorResult( types.Append( typeof( ObjectALevel1Conflict ) ), "Base class 'CK.StObj.Engine.Tests.SimpleObjects.ObjectA' has more than one concrete specialization: 'CK.StObj.Engine.Tests.SimpleObjectsTests.ObjectALevel1Conflict', 'CK.StObj.Engine.Tests.SimpleObjects.ObjectALevel3'." );
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
        TestHelper.GetFailedCollectorResult( [typeof( RealThatReferenceAutoIsError )], "StObjConstruct parameter 's' (n°1) for 'CK.StObj.Engine.Tests.SimpleObjectsTests+RealThatReferenceAutoIsError': Unable to resolve non optional." );
    }

    [Test]
    public void a_RealObject_that_references_an_auto_service_from_an_InjectObject_is_an_error()
    {
        TestHelper.GetFailedCollectorResult( [typeof( AmbientInject )], "Inject Object 'Service' of 'CK.StObj.Engine.Tests.SimpleObjectsTests+AmbientInject': CK.StObj.Engine.Tests.SimpleObjectsTests+Auto not found." );
    }

    [Test]
    public void Discovering_SimpleObjects()
    {
        var types = ThisAssembly.GetTypes()
                        .Where( t => t.IsClass )
                        .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects" );

        var map = TestHelper.GetSuccessfulCollectorResult( types ).EngineMap;
        Debug.Assert( map != null, "No initialization error." );

        IStObjResult oa = map.StObjs.ToHead( typeof( ObjectA ) )!;
        oa.Container!.ClassType.ShouldBe( typeof( PackageForAB ) );
        oa.LeafSpecialization.ClassType.ShouldBe( typeof( ObjectALevel3 ) );

        IStObjResult oa1 = map.StObjs.ToHead( typeof( ObjectALevel1 ) )!;
        oa1.Generalization.ShouldBeSameAs( oa );
        oa1.Container!.ClassType.ShouldBe( typeof( PackageForABLevel1 ) );

        IStObjResult oa2 = map.StObjs.ToHead( typeof( ObjectALevel2 ) )!;
        oa2.Generalization.ShouldBeSameAs( oa1 );
        oa2.Container!.ClassType.ShouldBe( typeof( PackageForABLevel1 ), "Inherited." );

        IStObjResult oa3 = map.StObjs.ToHead( typeof( ObjectALevel3 ) )!;
        oa3.Generalization.ShouldBeSameAs( oa2 );
        oa3.Container!.ClassType.ShouldBe( typeof( PackageForABLevel1 ), "Inherited." );
        oa.RootGeneralization.ClassType.ShouldBe( typeof( ObjectA ) );
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

            TestHelper.GetSuccessfulCollectorResult( types );
        }

        using( TestHelper.Monitor.OpenInfo( "ObjectALevel4 class (specializes ObjectALevel3 and use IAbstractionBOnLevel2)." ) )
        {
            var types = ThisAssembly.GetTypes()
                            .Where( t => t.IsClass )
                            .Where( t => t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects"
                                         || t.Namespace == "CK.StObj.Engine.Tests.SimpleObjects.WithLevel3" );

            TestHelper.GetSuccessfulCollectorResult( types );
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

            TestHelper.GetFailedCollectorResult( types, "Cycle detected: ↳ CK.StObj.Engine.Tests.SimpleObjects.ObjectBLevel1 ⊏ CK.StObj.Engine.Tests.SimpleObjects.PackageForABLevel1 ↟ CK.StObj.Engine.Tests.SimpleObjects.PackageForAB ⊐ CK.StObj.Engine.Tests.SimpleObjects.WithLevel3.Cycles.ObjectBLevel3_InPackageForAB ↟ CK.StObj.Engine.Tests.SimpleObjects.WithLevel3.ObjectBLevel2 ↟ CK.StObj.Engine.Tests.SimpleObjects.ObjectBLevel1." );
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
            TestHelper.GetFailedCollectorResult( [typeof( ObjectXNeedsY ), typeof( ObjectYNeedsX )],
                "Cycle detected: ↳ CK.StObj.Engine.Tests.SimpleObjectsTests+ObjectXNeedsY ⇀ CK.StObj.Engine.Tests.SimpleObjectsTests+ObjectYNeedsX ⇀ CK.StObj.Engine.Tests.SimpleObjectsTests+ObjectXNeedsY." );
        }
    }

    [Test]
    public void Missing_reference()
    {
        using( TestHelper.Monitor.OpenInfo( "ObjectXNeedsY without ObjectYNeedsX." ) )
        {
            TestHelper.GetFailedCollectorResult( [typeof( ObjectXNeedsY )],
                "StObjConstruct parameter 'other' (n°1) for 'CK.StObj.Engine.Tests.SimpleObjectsTests+ObjectXNeedsY': Unable to resolve non optional." );
        }
    }

    [Test]
    public void IActivityMonitor_injected_in_the_StObjConstruct_is_the_Setup_monitor()
    {
        using( TestHelper.Monitor.OpenInfo( "ConsoleMonitor injection (and optional parameter)." ) )
        {
            var types = new[] { typeof( SimpleObjects.LoggerInjection.LoggerInjected ) };

            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SimpleObjects.LoggerInjection.LoggerInjected )] ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            IStObjResult theObject = map.StObjs.ToLeaf( typeof( SimpleObjects.LoggerInjection.LoggerInjected ) )!;
            Assert.That( theObject, Is.Not.Null );
            Assert.That( theObject.FinalImplementation.Implementation, Is.Not.Null.And.InstanceOf<SimpleObjects.LoggerInjection.LoggerInjected>() );
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
    public async Task StObjConstruct_StObjInitialize_RegisterStartupServices_and_ConfigureServices_of_the_hierarchy_are_called_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( Dep0 ), typeof( Dep1 ), typeof( Dep2 ), typeof( Defined ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Services.GetService<SuperDef>().ShouldBeNull( "This is a SuperDefiner. It is NOT a real object." );
        auto.Services.GetService<Def>().ShouldBeNull( "This is SuperDefiner direct specialization. It is NOT a real object." );
        var defined = auto.Services.GetRequiredService<Defined>();
        defined.Dep0.ShouldNotBeNull();
        defined.Dep1.ShouldNotBeNull();
        defined.Dep2.ShouldNotBeNull();
        defined.StObjInitializeCallCount.ShouldBe( 3 );
        defined.RegisterStartupServicesCallCount.ShouldBe( 3 );
        defined.ConfigureServicesCallCount.ShouldBe( 3 );
    }


    #region Buggy & Valid Model

    [RealObject( ItemKind = DependentItemKindSpec.Container )]
    public class C1 : IRealObject
    {
    }

    [RealObject( Container = typeof( C1 ), ItemKind = DependentItemKindSpec.Container )]
    public class C2InC1 : IRealObject
    {
    }

    public class C3InC2SpecializeC1 : C1
    {
        void StObjConstruct( [Container] C2InC1 c2 )
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


        TestHelper.GetFailedCollectorResult( types, "Cycle detected: " );
    }

    [RealObject( ItemKind = DependentItemKindSpec.Container, Container = typeof( C2InC1 ), Children = [typeof( C1 )] )]
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

        TestHelper.GetFailedCollectorResult( types, "Cycle detected: " );
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

        TestHelper.GetSuccessfulCollectorResult( types );

    }
    #endregion

    public abstract class MissingAutoImplementation : IRealObject
    {
        public abstract void Nop();
    }

    [Test]
    public void missing_AutoImplementation_attributes()
    {
        var m = TestHelper.GetSuccessfulCollectorResult( [typeof( MissingAutoImplementation )] ).EngineMap;
        Debug.Assert( m != null );
        m.StObjs.FinalImplementations.ShouldNotContain( i => i.FinalImplementation.Implementation is MissingAutoImplementation );
    }
}
