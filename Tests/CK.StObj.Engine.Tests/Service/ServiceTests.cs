using CK.Core;
using CK.Testing;
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
            var collector = TestHelper.CreateTypeCollector( typeof( SampleService ), typeof( SampleService2 ) );
            var map = TestHelper.GetSuccessfulCollectorResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.Mappings[typeof( ISampleService )].ClassType.Should().Be( typeof( SampleService2 ) );
            map.Services.Mappings[typeof( SampleService )].ClassType.Should().Be( typeof( SampleService ) );
        }

        [Local.ReplaceAutoService( "CK.StObj.Engine.Tests.Service.ServiceTests+SampleService2, CK.StObj.Engine.Tests" )]
        public class SampleService3 : ISampleService
        {
        }

        [Test]
        public void ReplaceAutoService_works_with_assembly_qualified_name_and_locally_defined_attribute()
        {
            var collector = TestHelper.CreateTypeCollector( typeof( SampleService ), typeof( SampleService2 ), typeof( SampleService3 ) );
            var map = TestHelper.GetSuccessfulCollectorResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.Mappings[typeof( ISampleService )].ClassType.Should().Be( typeof( SampleService3 ) );
            map.Services.Mappings[typeof( SampleService2 )].ClassType.Should().Be( typeof( SampleService2 ) );
            map.Services.Mappings[typeof( SampleService )].ClassType.Should().Be( typeof( SampleService ) );
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
            var collector = TestHelper.CreateTypeCollector( typeof( UseActivityMonitor ) );
            TestHelper.GetFailedCollectorResult( collector, "is marked as IsSingleton but parameter 'm' of type 'IActivityMonitor' in constructor is Scoped." );
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
                var collector = TestHelper.CreateTypeCollector( typeof( Obj ) );
                using var auto = TestHelper.CreateSingleBinPathAutomaticServices( collector );

                auto.LoadResult.Map.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeOfType<Obj>();
                auto.LoadResult.Map.StObjs.Obtain<Obj>().Should().BeOfType<Obj>();
                auto.LoadResult.Map.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
                // On built ServiceProvider.
                var o = auto.Services.GetRequiredService<Obj>();
                auto.Services.GetRequiredService<ISampleService>().Should().BeSameAs( o );
            }
            {
                var collector = TestHelper.CreateTypeCollector( typeof( ObjInvalid ) );
                TestHelper.GetFailedSingleBinPathAutomaticServices( collector, "IRealObject interface cannot be a IAutoService (type is an interface)." );
            }
        }

        public abstract class ObjSpec : Obj
        {
        }

        [Test]
        public void a_RealObject_class_and_IAutoService_with_specialization()
        {
            var collector = TestHelper.CreateTypeCollector( typeof( ObjSpec ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( collector );
            auto.LoadResult.Map.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeAssignableTo<ObjSpec>();
            auto.LoadResult.Map.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
            auto.LoadResult.Map.StObjs.Obtain<Obj>().Should().BeAssignableTo<ObjSpec>();
            auto.LoadResult.Map.StObjs.Obtain<ObjSpec>().Should().BeAssignableTo<ObjSpec>();
            // On the built ServiceProvider.
            var o = auto.Services.GetRequiredService<Obj>();
            auto.Services.GetRequiredService<ObjSpec>().Should().BeSameAs( o );
            auto.Services.GetRequiredService<ISampleService>().Should().BeSameAs( o );
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
            var collector = TestHelper.CreateTypeCollector( typeof( ObjSpecFinal ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( collector );
            // On generated data.
            auto.LoadResult.Map.Services.ObjectMappings[typeof( ISampleService )].Implementation.Should().BeAssignableTo<ObjSpecFinal>();
            auto.LoadResult.Map.Services.ObjectMappings[typeof( ISampleServiceSpec )].Implementation.Should().BeAssignableTo<ObjSpecFinal>();
            auto.LoadResult.Map.StObjs.Obtain<ISampleService>().Should().BeNull( "ISampleService is a Service." );
            auto.LoadResult.Map.StObjs.Obtain<ISampleServiceSpec>().Should().BeNull( "ISampleServiceSpec is a Service." );
            auto.LoadResult.Map.StObjs.Obtain<Obj>().Should().BeAssignableTo<ObjSpecFinal>();
            auto.LoadResult.Map.StObjs.Obtain<ObjSpec>().Should().BeAssignableTo<ObjSpecFinal>();
            auto.LoadResult.Map.StObjs.Obtain<ObjSpecIntermediate>().Should().BeAssignableTo<ObjSpecFinal>();
            // On build ServiceProvider.
            var o = auto.Services.GetRequiredService<ObjSpecFinal>();
            auto.Services.GetRequiredService<Obj>().Should().BeSameAs( o );
            auto.Services.GetRequiredService<ObjSpec>().Should().BeSameAs( o );
            auto.Services.GetRequiredService<ObjSpecIntermediate>().Should().BeSameAs( o );
            auto.Services.GetRequiredService<ISampleService>().Should().BeSameAs( o );
            auto.Services.GetRequiredService<ISampleServiceSpec>().Should().BeSameAs( o );
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

        // There is no need for an explicit Replacement here since the IDerived being a IAutoService,
        // it has to be satisfied and can be satisfied only by ODep.
        //[ReplaceAutoService(typeof(OBase))]
        public abstract class ODep : IRealObject, IDerived
        {
            void StObjConstruct( OBase o ) { }
        }

        [Test]
        public void service_can_be_implemented_by_RealObjects()
        {
            var collector = TestHelper.CreateTypeCollector( typeof( ODep ), typeof( OBase ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( collector );
            var oDep = auto.Services.GetRequiredService<ODep>();
            auto.Services.GetRequiredService<IBase>().Should().BeSameAs( oDep );
            auto.Services.GetRequiredService<IDerived>().Should().BeSameAs( oDep );
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
            var collector = TestHelper.CreateTypeCollector( typeof( StupidService1 ), typeof( StupidService2 ) );
            TestHelper.GetFailedCollectorResult( collector, "Cyclic constructor dependency detected:" );
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
            var collector = TestHelper.CreateTypeCollector( typeof( StupidServiceViaInterface1 ), typeof( StupidServiceViaInterface2 ) );
            TestHelper.GetFailedCollectorResult( collector, "Service class dependency cycle detected:" );
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
            var collector = TestHelper.CreateTypeCollector( typeof( A ), typeof( B ), typeof( SqlCallContext ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( collector );

            auto.LoadResult.Map.Services.Mappings[typeof( IB )].IsScoped.Should().BeTrue();
            auto.LoadResult.Map.Services.Mappings[typeof( A )].IsScoped.Should().BeTrue();
            auto.LoadResult.Map.Services.Mappings[typeof( IA )].IsScoped.Should().BeTrue();

            // The IServiceProvider is a Scope: it resolves and stores Scoped services at its (root) level.
            var rootA = auto.Services.GetRequiredService<A>();
            rootA.Should().Be( auto.Services.GetRequiredService<IA>() ).And.NotBeNull();
            var rootB = auto.Services.GetRequiredService<B>();
            rootB.Should().Be( auto.Services.GetRequiredService<IB>() ).And.NotBeNull();
            rootB.Ctx.Should().Be( auto.Services.GetRequiredService<ISqlCallContext>() ).And.NotBeNull();

            using( var scope = auto.Services.CreateScope() )
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
            var collector = TestHelper.CreateTypeCollector( typeof( SampleServiceGenerated ), typeof( SampleService ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( collector );
            auto.Services.GetRequiredService<ISampleService>().Should().BeOfType<SampleService>();
        }


    }
}
