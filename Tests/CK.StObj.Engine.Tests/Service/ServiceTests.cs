using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    namespace Local
    {
        [AttributeUsage( AttributeTargets.Class, AllowMultiple = true, Inherited = false )]
        class ReplaceAutoServiceAttribute : Attribute
        {
            public ReplaceAutoServiceAttribute( string replacedAssemblyQualifiedName )
            {
            }
        }
    }

    [TestFixture]
    public class ServiceTests 
    {
        public interface ISampleService : IAutoService
        {
        }

        public class SampleService : ISampleService
        {
        }

        [ReplaceAutoService( typeof( SampleService ) )]
        public class SampleService2 : ISampleService
        {
        }

        [Test]
        public void ReplaceAutoService_works_with_type()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SampleService ) );
            collector.RegisterType( typeof( SampleService2 ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.SimpleMappings[typeof( ISampleService )].ClassType.Should().Be( typeof( SampleService2 ) );
            map.Services.SimpleMappings[typeof( SampleService )].ClassType.Should().Be( typeof( SampleService ) );
        }

        [Local.ReplaceAutoService( "CK.StObj.Engine.Tests.Service.ServiceTests+SampleService2, CK.StObj.Engine.Tests" )]
        public class SampleService3 : ISampleService
        {
        }

        [Test]
        public void ReplaceAutoService_works_with_assembly_qualified_name_and_locally_defined_attribute()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SampleService ) );
            collector.RegisterType( typeof( SampleService2 ) );
            collector.RegisterType( typeof( SampleService3 ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.SimpleMappings[typeof( ISampleService )].ClassType.Should().Be( typeof( SampleService3 ) );
            map.Services.SimpleMappings[typeof( SampleService2 )].ClassType.Should().Be( typeof( SampleService2 ) );
            map.Services.SimpleMappings[typeof( SampleService )].ClassType.Should().Be( typeof( SampleService ) );
        }


        public class UseActivityMonitor : ISingletonAutoService
        {
            public UseActivityMonitor( IActivityMonitor m )
            {
            }
        }

        [Test]
        public void IActivityMonitor_is_Scoped_by_default()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( UseActivityMonitor ) );
            TestHelper.GetFailedResult( collector );
        }

        public class Obj : IRealObject, ISampleService
        {
        }


        public interface IInvalidInterface : IRealObject, ISampleService
        {
        }

        public class ObjInvalid : IInvalidInterface
        {
        }


        [Test]
        public void a_RealObject_class_can_be_an_IAutoService_but_an_interface_cannot()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( Obj ) );
                var (collectorResult, map, reg, sp) = TestHelper.GetAutomaticServices( collector );
                Debug.Assert( collectorResult.EngineMap != null, "No initialization error." );

                // On runtime data.
                collectorResult.EngineMap.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeOfType<Obj>();
                collectorResult.EngineMap.StObjs.Obtain<Obj>().Should().BeOfType<Obj>();
                collectorResult.EngineMap.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
                // On generated data.
                map.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeOfType<Obj>();
                map.StObjs.Obtain<Obj>().Should().BeOfType<Obj>();
                map.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
                // On built ServiceProvider.
                var o = sp.GetRequiredService<Obj>();
                sp.GetRequiredService<ISampleService>().Should().BeSameAs( o );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.RegisterType( typeof( ObjInvalid ) );
                TestHelper.GetFailedResult( collector );
            }
        }

        public abstract class ObjSpec : Obj
        {
        }

        [Test]
        public void a_RealObject_class_and_IAutoService_with_specialization()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ObjSpec ) );
            var (collectorResult, map, reg, sp) = TestHelper.GetAutomaticServices( collector );
            Debug.Assert( collectorResult.EngineMap != null, "No initialization error." );
            // On runtime data.
            collectorResult.EngineMap.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeAssignableTo<ObjSpec>();
            collectorResult.EngineMap.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
            collectorResult.EngineMap.StObjs.Obtain<Obj>().Should().BeAssignableTo<ObjSpec>();
            collectorResult.EngineMap.StObjs.Obtain<ObjSpec>().Should().BeAssignableTo<ObjSpec>();
            // On generated data.
            map.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeAssignableTo<ObjSpec>();
            map.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
            map.StObjs.Obtain<Obj>().Should().BeAssignableTo<ObjSpec>();
            map.StObjs.Obtain<ObjSpec>().Should().BeAssignableTo<ObjSpec>();
            // On the built ServiceProvider.
            var o = sp.GetRequiredService<Obj>();
            sp.GetRequiredService<ObjSpec>().Should().BeSameAs( o );
            sp.GetRequiredService<ISampleService>().Should().BeSameAs( o );
        }

        public interface ISampleServiceSpec : ISampleService
        {
        }

        // Intermediate concrete class: this doesn't change anything.
        public class ObjSpecIntermediate : ObjSpec, ISampleServiceSpec
        {
        }

        public abstract class ObjSpecFinal : ObjSpecIntermediate
        {
        }

        [Test]
        public void a_RealObject_class_and_IAutoService_with_deep_specializations()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ObjSpecFinal ) );
            var (collectorResult, map, reg, sp) = TestHelper.GetAutomaticServices( collector );
            Debug.Assert( collectorResult.EngineMap != null, "No initialization error." );
            // On runtime data.
            collectorResult.EngineMap.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeAssignableTo<ObjSpecFinal>();
            collectorResult.EngineMap.Services.ObjectMappings[typeof( ISampleServiceSpec )].Implementation.Should().BeAssignableTo<ObjSpecFinal>();
            collectorResult.EngineMap.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
            collectorResult.EngineMap.StObjs.Obtain<ISampleServiceSpec>().Should().BeNull( "ISampleServiceSpec is a Service." );
            collectorResult.EngineMap.StObjs.Obtain<Obj>().Should().BeAssignableTo<ObjSpecFinal>();
            collectorResult.EngineMap.StObjs.Obtain<ObjSpec>().Should().BeAssignableTo<ObjSpecFinal>();
            collectorResult.EngineMap.StObjs.Obtain<ObjSpecIntermediate>().Should().BeAssignableTo<ObjSpecFinal>();
            // On generated data.
            map.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeAssignableTo<ObjSpecFinal>();
            map.Services.ObjectMappings[typeof( ISampleServiceSpec )].Implementation.Should().BeAssignableTo<ObjSpecFinal>();
            map.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
            map.StObjs.Obtain<ISampleServiceSpec>().Should().BeNull( "ISampleServiceSpec is a Service." );
            map.StObjs.Obtain<Obj>().Should().BeAssignableTo<ObjSpecFinal>();
            map.StObjs.Obtain<ObjSpec>().Should().BeAssignableTo<ObjSpecFinal>();
            map.StObjs.Obtain<ObjSpecIntermediate>().Should().BeAssignableTo<ObjSpecFinal>();
            // On build ServiceProvider.
            var o = sp.GetRequiredService<ObjSpecFinal>();
            sp.GetRequiredService<Obj>().Should().BeSameAs( o );
            sp.GetRequiredService<ObjSpec>().Should().BeSameAs( o );
            sp.GetRequiredService<ObjSpecIntermediate>().Should().BeSameAs( o );
            sp.GetRequiredService<ISampleService>().Should().BeSameAs( o );
            sp.GetRequiredService<ISampleServiceSpec>().Should().BeSameAs( o );
        }

        public interface IBase : IAutoService
        {
        }

        public interface IDerived : IBase
        {
        }

        public abstract class OBase : IRealObject, IBase
        {
        }

        // There is no need for an explicit Replacement here since the IDerived being an IAutoService,
        // it has to be satisfied and can be satisfied only by ODep.
        //[ReplaceAutoService(typeof(OBase))]
        public abstract class ODep : IRealObject, IDerived
        {
            void StObjConstruct( OBase o ) { }
        }

        [Test]
        public void service_can_be_implemented_by_RealObjects()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ODep ) );
            collector.RegisterType( typeof( OBase ) );
            var sp = TestHelper.GetAutomaticServices( collector ).Services;
            var oDep = sp.GetRequiredService<ODep>();
            sp.GetRequiredService<IBase>().Should().BeSameAs( oDep );
            sp.GetRequiredService<IDerived>().Should().BeSameAs( oDep );
        }


        public class StupidService1 : IBase
        {
            public StupidService1( StupidService2 stupid )
            {
            }

        }

        public class StupidService2 : ISampleService
        {
            public StupidService2( StupidService1 b )
            {
            }
        }

        [Test]
        public void services_class_cyclic_dependencies_are_detected()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( StupidService1 ) );
            collector.RegisterType( typeof( StupidService2 ) );
            TestHelper.GetFailedResult( collector );
        }

        public class StupidServiceViaInterface1 : IBase
        {
            public StupidServiceViaInterface1( ISampleService stupid )
            {
            }

        }

        public class StupidServiceViaInterface2 : ISampleService
        {
            public StupidServiceViaInterface2( IBase b )
            {
            }
        }

        [Test]
        public void services_via_interfaces_cyclic_dependencies_are_detected()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( StupidServiceViaInterface1 ) );
            collector.RegisterType( typeof( StupidServiceViaInterface2 ) );
            TestHelper.GetFailedResult( collector );
        }

        #region issue https://gitlab.com/signature-code/CK-Setup/issues/3 (wrong repository :D).

        public interface ISqlCallContext : IScopedAutoService { }

        public class SqlCallContext : ISqlCallContext { }

        public interface IA : IAutoService { }
        public interface IB : IAutoService { }

        public class A : IA
        {
            public A( IB dep ) { }
        }

        public class B : IB
        {
            public B( ISqlCallContext dep ) { Ctx = dep; }

            public ISqlCallContext Ctx { get; }
        }

        [Test]
        public void scoped_dependency_detection()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( A ) );
            collector.RegisterType( typeof( B ) );
            collector.RegisterType( typeof( SqlCallContext ) );
            var r = TestHelper.GetAutomaticServices( collector );
            Debug.Assert( r.Result.EngineMap != null, "No initialization error." );
            r.Result.EngineMap.Services.SimpleMappings[typeof( IB )].IsScoped.Should().BeTrue();
            r.Result.EngineMap.Services.SimpleMappings[typeof( A )].IsScoped.Should().BeTrue();
            r.Result.EngineMap.Services.SimpleMappings[typeof( IA )].IsScoped.Should().BeTrue();

            // The IServiceProvider is a Scope: it resolves and stores Scoped services at its (root) level.
            var rootA = r.Services.GetRequiredService<A>();
            rootA.Should().Be( r.Services.GetRequiredService<IA>() ).And.NotBeNull();
            var rootB = r.Services.GetRequiredService<B>();
            rootB.Should().Be( r.Services.GetRequiredService<IB>() ).And.NotBeNull();
            rootB.Ctx.Should().Be( r.Services.GetRequiredService<ISqlCallContext>() ).And.NotBeNull();

            using( var scope = r.Services.CreateScope() )
            {
                var scopeA = scope.ServiceProvider.GetRequiredService<A>();
                scopeA.Should().NotBeSameAs( rootA ).And.NotBeNull();

                var scopeB = scope.ServiceProvider.GetRequiredService<B>();
                scopeB.Should().NotBeSameAs( rootB ).And.NotBeNull();
                scopeB.Ctx.Should().Be( scope.ServiceProvider.GetRequiredService<ISqlCallContext>() ).And.NotBeNull();

                scopeB.Ctx.Should().NotBeSameAs( rootB.Ctx );
            }

        }

        #endregion

        [CK.Core.StObjGen]
        public class SampleServiceGenerated : ISampleService
        {
        }

        [Test]
        public void StObjGen_attribute_excludes_the_type()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SampleServiceGenerated ) );
            collector.RegisterType( typeof( SampleService ) );
            var r = TestHelper.GetAutomaticServices( collector );
            r.Services.GetRequiredService<ISampleService>().Should().BeOfType<SampleService>();
        }


    }
}
