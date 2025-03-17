using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

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
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SampleService ), typeof( SampleService2 )] ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.Mappings[typeof( ISampleService )].ClassType.ShouldBe( typeof( SampleService2 ) );
            map.Services.Mappings[typeof( SampleService )].ClassType.ShouldBe( typeof( SampleService ) );
        }

        [Local.ReplaceAutoService( "CK.StObj.Engine.Tests.Service.ServiceTests+SampleService2, CK.StObj.Engine.Tests" )]
        public class SampleService3 : ISampleService
        {
        }

        [Test]
        public void ReplaceAutoService_works_with_assembly_qualified_name_and_locally_defined_attribute()
        {
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SampleService ), typeof( SampleService2 ), typeof( SampleService3 )] ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            map.Services.Mappings[typeof( ISampleService )].ClassType.ShouldBe( typeof( SampleService3 ) );
            map.Services.Mappings[typeof( SampleService2 )].ClassType.ShouldBe( typeof( SampleService2 ) );
            map.Services.Mappings[typeof( SampleService )].ClassType.ShouldBe( typeof( SampleService ) );
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
            TestHelper.GetFailedCollectorResult( [typeof( UseActivityMonitor )], "is marked as IsSingleton but parameter 'm' of type 'IActivityMonitor' in constructor is Scoped." );
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
        public async Task a_RealObject_class_can_be_an_IAutoService_but_an_interface_cannot_Async()
        {
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.Types.Add( typeof( Obj ) );
                await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

                auto.Map.Services.ObjectMappings[typeof( ISampleService )].Implementation.ShouldBeOfType<Obj>();
                auto.Map.StObjs.Obtain<Obj>().ShouldBeOfType<Obj>();
                auto.Map.StObjs.Obtain<ISampleService>().ShouldBeNull( "ISampleService is a Service." );
                // On built ServiceProvider.
                var o = auto.Services.GetRequiredService<Obj>();
                auto.Services.GetRequiredService<ISampleService>().ShouldBeSameAs( o );
            }
            {
                var configuration = TestHelper.CreateDefaultEngineConfiguration();
                configuration.FirstBinPath.Types.Add( typeof( ObjInvalid ) );
                await configuration.GetFailedAutomaticServicesAsync( "IRealObject interface cannot be a IAutoService (type is an interface)." );
            }
        }

        public abstract class ObjSpec : Obj
        {
        }

        [Test]
        public async Task a_RealObject_class_and_IAutoService_with_specialization_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ObjSpec ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Map.Services.ObjectMappings[typeof( ISampleService )].Implementation.ShouldBeAssignableTo<ObjSpec>();
            auto.Map.StObjs.Obtain<ISampleService>().ShouldBeNull( "ISampleService is a Service." );
            auto.Map.StObjs.Obtain<Obj>().ShouldBeAssignableTo<ObjSpec>();
            auto.Map.StObjs.Obtain<ObjSpec>().ShouldBeAssignableTo<ObjSpec>();
            // On the built ServiceProvider.
            var o = auto.Services.GetRequiredService<Obj>();
            auto.Services.GetRequiredService<ObjSpec>().ShouldBeSameAs( o );
            auto.Services.GetRequiredService<ISampleService>().ShouldBeSameAs( o );
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
        public async Task a_RealObject_class_and_IAutoService_with_deep_specializations_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ObjSpecFinal ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            // On generated data.
            auto.Map.Services.ObjectMappings[typeof( ISampleService )].Implementation.ShouldBeAssignableTo<ObjSpecFinal>();
            auto.Map.Services.ObjectMappings[typeof( ISampleServiceSpec )].Implementation.ShouldBeAssignableTo<ObjSpecFinal>();
            auto.Map.StObjs.Obtain<ISampleService>().ShouldBeNull( "ISampleService is a Service." );
            auto.Map.StObjs.Obtain<ISampleServiceSpec>().ShouldBeNull( "ISampleServiceSpec is a Service." );
            auto.Map.StObjs.Obtain<Obj>().ShouldBeAssignableTo<ObjSpecFinal>();
            auto.Map.StObjs.Obtain<ObjSpec>().ShouldBeAssignableTo<ObjSpecFinal>();
            auto.Map.StObjs.Obtain<ObjSpecIntermediate>().ShouldBeAssignableTo<ObjSpecFinal>();
            // On build ServiceProvider.
            var o = auto.Services.GetRequiredService<ObjSpecFinal>();
            auto.Services.GetRequiredService<Obj>().ShouldBeSameAs( o );
            auto.Services.GetRequiredService<ObjSpec>().ShouldBeSameAs( o );
            auto.Services.GetRequiredService<ObjSpecIntermediate>().ShouldBeSameAs( o );
            auto.Services.GetRequiredService<ISampleService>().ShouldBeSameAs( o );
            auto.Services.GetRequiredService<ISampleServiceSpec>().ShouldBeSameAs( o );
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
        public async Task service_can_be_implemented_by_RealObjects_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( ODep ), typeof( OBase ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            var oDep = auto.Services.GetRequiredService<ODep>();
            auto.Services.GetRequiredService<IBase>().ShouldBeSameAs( oDep );
            auto.Services.GetRequiredService<IDerived>().ShouldBeSameAs( oDep );
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
            TestHelper.GetFailedCollectorResult( [typeof( StupidService1 ), typeof( StupidService2 )], "Cyclic constructor dependency detected:" );
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
            TestHelper.GetFailedCollectorResult( [typeof( StupidServiceViaInterface1 ), typeof( StupidServiceViaInterface2 )], "Service class dependency cycle detected:" );
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
        public async Task scoped_dependency_detection_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( A ), typeof( B ), typeof( SqlCallContext ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Map.Services.Mappings[typeof( IB )].IsScoped.ShouldBeTrue();
            auto.Map.Services.Mappings[typeof( A )].IsScoped.ShouldBeTrue();
            auto.Map.Services.Mappings[typeof( IA )].IsScoped.ShouldBeTrue();

            // The IServiceProvider is a Scope: it resolves and stores Scoped services at its (root) level.
            var rootA = auto.Services.GetRequiredService<A>();
            rootA.ShouldBe( auto.Services.GetRequiredService<IA>() );
            var rootB = auto.Services.GetRequiredService<B>();
            rootB.ShouldBe( auto.Services.GetRequiredService<IB>() );
            rootB.Ctx.ShouldBe( auto.Services.GetRequiredService<ISqlCallContext>() );

            using( var scope = auto.Services.CreateScope() )
            {
                var scopeA = scope.ServiceProvider.GetRequiredService<A>();
                scopeA.ShouldNotBeSameAs( rootA );

                var scopeB = scope.ServiceProvider.GetRequiredService<B>();
                scopeB.ShouldNotBeSameAs( rootB );
                scopeB.Ctx.ShouldBe( scope.ServiceProvider.GetRequiredService<ISqlCallContext>() );

                scopeB.Ctx.ShouldNotBeSameAs( rootB.Ctx );
            }
        }

        #endregion

        [CK.Core.StObjGen]
        public class SampleServiceGenerated : ISampleService
        {
        }

        [Test]
        public async Task StObjGen_attribute_excludes_the_type_Async()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Types.Add( typeof( SampleServiceGenerated ), typeof( SampleService ) );
            await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

            auto.Services.GetRequiredService<ISampleService>().ShouldBeOfType<SampleService>();
        }


    }
}
