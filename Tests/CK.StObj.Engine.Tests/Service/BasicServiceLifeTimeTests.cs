using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class BasicServiceLifeTimeTests
    {
        public interface IServiceRegistered : IScopedAutoService
        {
        }

        public interface IAutoService { }
        public interface IScopedAutoService { }
        public interface ISingletonAutoService { }

        // These SimpleClass are tagged with locally named interfaces.
        public class SimpleClassSingleton : ISingletonAutoService { }
        public class SimpleClassScoped : IScopedAutoService { }
        public class SimpleClassAmbient : IAutoService { }

        [Test]
        public void class_scope_simple_tests()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( SimpleClassScoped ) );
            collector.RegisterType( typeof( SimpleClassAmbient ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.SimpleMappings[typeof( SimpleClassSingleton )].IsScoped.Should().BeFalse();
            r.Services.SimpleMappings[typeof( SimpleClassScoped )].IsScoped.Should().BeTrue();
            r.Services.SimpleMappings[typeof( SimpleClassAmbient )].IsScoped.Should().BeFalse();
        }

        public class BuggyDoubleScopeClassAmbient : IScopedAutoService, Core.ISingletonAutoService { }

        [Test]
        public void a_class_with_both_scopes_is_an_error()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( BuggyDoubleScopeClassAmbient ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 1 );
            TestHelper.GetFailedResult( collector );
        }

        public class LifetimeErrorClassAmbientBecauseOfScoped : Core.ISingletonAutoService
        {
            public LifetimeErrorClassAmbientBecauseOfScoped( SimpleClassScoped d )
            {
            }
        }

        [Test]
        public void a_singleton_that_depends_on_scoped_is_an_error()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SimpleClassScoped ) );
            collector.RegisterType( typeof( LifetimeErrorClassAmbientBecauseOfScoped ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            TestHelper.GetFailedResult( collector );
        }

        public interface IExternalService { }

        public class LifetimeOfExternalBoostToSingleton : Core.ISingletonAutoService
        {
            public LifetimeOfExternalBoostToSingleton( IExternalService e )
            {
            }
        }

        [Test]
        public void a_singleton_that_depends_on_an_unknwon_external_is_not_possible()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( LifetimeOfExternalBoostToSingleton ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            TestHelper.GetFailedResult( collector );
        }

        [Test]
        public void a_singleton_that_depends_on_external_that_is_defined_as_a_singleton_is_fine()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( IExternalService ), AutoServiceKind.IsSingleton );
            collector.RegisterType( typeof( LifetimeOfExternalBoostToSingleton ) );
            TestHelper.GetSuccessfulResult( collector );
        }

        [Test]
        public void a_singleton_that_depends_on_external_that_is_defined_as_a_Scoped_is_an_error()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( IExternalService ), AutoServiceKind.IsScoped );
            collector.RegisterType( typeof( LifetimeOfExternalBoostToSingleton ) );
            TestHelper.GetFailedResult( collector );
        }

        public class SingletonThatDependsOnSingleton : Core.ISingletonAutoService
        {
            public SingletonThatDependsOnSingleton( SimpleClassSingleton e )
            {
            }
        }

        [Test]
        public void a_singleton_that_depends_on_singleton()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( SingletonThatDependsOnSingleton ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            TestHelper.GetSuccessfulResult( collector );
        }

        public class AmbientThatDependsOnSingleton : IAutoService
        {
            public AmbientThatDependsOnSingleton( SimpleClassSingleton e )
            {
            }
        }

        [Test]
        public void an_auto_service_that_depends_only_on_singleton_is_singleton()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.SimpleMappings[typeof( AmbientThatDependsOnSingleton )].IsScoped.Should().BeFalse();
        }

        public interface IAmbientThatDependsOnNothing : IAutoService { }

        public class AmbientThatDependsOnNothing : IAmbientThatDependsOnNothing { }

        [Test]
        public void an_auto_service_that_depends_on_nothing_is_singleton()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( AmbientThatDependsOnNothing ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.SimpleMappings[typeof( IAmbientThatDependsOnNothing )].IsScoped.Should().BeFalse();
            r.Services.SimpleMappings[typeof( AmbientThatDependsOnNothing )].IsScoped.Should().BeFalse();
        }

        public class AmbientThatDependsOnExternal : IAutoService
        {
            public AmbientThatDependsOnExternal( IExternalService e )
            {
            }
        }

        [Test]
        public void an_auto_service_that_depends_on_an_external_service_is_Scoped()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( AmbientThatDependsOnExternal ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.SimpleMappings[typeof( AmbientThatDependsOnExternal )].IsScoped.Should().BeTrue();
        }

        public interface ISampleRealObject : IRealObject { }

        public class SampleRealObject : ISampleRealObject { }

        public interface ISamplePoco : IPoco { }

        public class AmbientThatWillBeResolvedAsSingleton : IAutoService
        {
            public AmbientThatWillBeResolvedAsSingleton( ISampleRealObject c )
            {
            }
        }


        public class AmbientThatDependsOnAllKindOfSingleton : IAutoService
        {
            public AmbientThatDependsOnAllKindOfSingleton(
                IExternalService e,
                IPocoFactory<ISamplePoco> pocoFactory,
                ISampleRealObject contract,
                AmbientThatDependsOnSingleton d,
                SimpleClassSingleton s,
                AmbientThatWillBeResolvedAsSingleton other )
            {
            }
        }

        [Test]
        public void an_auto_service_that_depends_on_all_kind_of_singletons_is_singleton()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( IExternalService ), AutoServiceKind.IsSingleton );
            collector.RegisterType( typeof( AmbientThatDependsOnAllKindOfSingleton ) );
            collector.RegisterType( typeof( AmbientThatDependsOnExternal ) );
            collector.RegisterType( typeof( SampleRealObject ) );
            collector.RegisterType( typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( AmbientThatWillBeResolvedAsSingleton ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.SimpleMappings[typeof( AmbientThatDependsOnAllKindOfSingleton )].IsScoped.Should().BeFalse();
        }

        public interface IOtherExternalService { }

        public class AmbientThatDependsOnAnotherExternalService : IAutoService
        {
            public AmbientThatDependsOnAnotherExternalService( IOtherExternalService o )
            {
            }
        }

        public class AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService : IAutoService
        {
            public AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService(
                IExternalService e,
                IPocoFactory<ISamplePoco> pocoFactory,
                ISampleRealObject contract,
                AmbientThatDependsOnSingleton d,
                SimpleClassSingleton s,
                AmbientThatDependsOnAnotherExternalService o )
            {
            }
        }

        [TestCase( "UnknwonLifetimeExternalService" )]
        [TestCase( "WithSingletonLifetimeOnExternalService" )]
        public void a_singleton_service_that_depends_on_all_kind_of_singletons_is_singleton( string mode )
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( IExternalService ), AutoServiceKind.IsSingleton );
            if( mode == "WithSingletonLifetimeOnExternalService" )
            {
                collector.SetAutoServiceKind( typeof( IOtherExternalService ), AutoServiceKind.IsSingleton );
            }
            collector.RegisterType( typeof( AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService ) );
            collector.RegisterType( typeof( AmbientThatDependsOnExternal ) );
            collector.RegisterType( typeof( SampleRealObject ) );
            collector.RegisterType( typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( AmbientThatDependsOnAnotherExternalService ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            bool isScoped = r.Services.SimpleMappings[typeof( AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService )].IsScoped;
            isScoped.Should().Be( mode == "UnknwonLifetimeExternalService" );
        }


        public interface IExternalService2 : IAmbientThatDependsOnNothing { }

        public interface IExternalService3 : IExternalService2 { }

        public class ExtS : IExternalService3 { }

        [Test]
        public void SetAutoServiceKind_application_order_doesnt_matter_on_interfaces()
        {
            // Because all interfaces are flattened on Types that support them.
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IAmbientThatDependsOnNothing ), AutoServiceKind.IsScoped );
                collector.SetAutoServiceKind( typeof( IExternalService2 ), AutoServiceKind.IsProcessService );
                collector.SetAutoServiceKind( typeof( IExternalService3 ), AutoServiceKind.IsMarshallable );
                collector.RegisterType( typeof( ExtS ) );
                var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
                Debug.Assert( r != null, "No initialization error." );
                r.Services.SimpleMappings[typeof( ExtS )].AutoServiceKind.Should().Be( AutoServiceKind.IsScoped
                                                                                        | AutoServiceKind.IsProcessService );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( IExternalService2 ), AutoServiceKind.IsProcessService );
                collector.SetAutoServiceKind( typeof( IExternalService3 ), AutoServiceKind.IsMarshallable );
                collector.SetAutoServiceKind( typeof( IAmbientThatDependsOnNothing ), AutoServiceKind.IsScoped );
                collector.RegisterType( typeof( ExtS ) );
                var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
                Debug.Assert( r != null, "No initialization error." );
                r.Services.SimpleMappings[typeof( ExtS )].AutoServiceKind.Should().Be( AutoServiceKind.IsScoped
                                                                                        | AutoServiceKind.IsProcessService );
            }
        }



        public class CBase1 : IAutoService { }

        public class CBase2 : CBase1 { }

        public class CBase3 : CBase2 { }

        public class ExtSC : CBase3 { }

        [Test]
        public void SetAutoServiceKind_application_order_matters_on_inheritance_class_chain()
        {
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( CBase1 ), AutoServiceKind.IsScoped );
                collector.SetAutoServiceKind( typeof( CBase2 ), AutoServiceKind.IsProcessService );
                collector.SetAutoServiceKind( typeof( CBase3 ), AutoServiceKind.IsMarshallable );
                collector.RegisterType( typeof( ExtSC ) );
                var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
                Debug.Assert( r != null, "No initialization error." );
                r.Services.SimpleMappings[typeof( ExtSC )].AutoServiceKind.Should().Be( AutoServiceKind.IsScoped
                                                                                        | AutoServiceKind.IsProcessService );
            }
            {
                var collector = TestHelper.CreateStObjCollector();
                collector.SetAutoServiceKind( typeof( CBase2 ), AutoServiceKind.IsProcessService );
                collector.SetAutoServiceKind( typeof( CBase3 ), AutoServiceKind.IsMarshallable );
                // CBase1 is set last: without the "base type flattening" step, the ExtSC below would be a Singleton!
                collector.SetAutoServiceKind( typeof( CBase1 ), AutoServiceKind.IsScoped );
                collector.RegisterType( typeof( ExtSC ) );
                var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
                Debug.Assert( r != null, "No initialization error." );
                r.Services.SimpleMappings[typeof( ExtSC )].AutoServiceKind.Should().Be( AutoServiceKind.IsSingleton
                                                                                        | AutoServiceKind.IsProcessService );
            }
        }

        [Test]
        public void SetAutoServiceKind_a_class_doesnt_mean_registering_it()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( typeof( CBase1 ), AutoServiceKind.IsScoped );
            collector.SetAutoServiceKind( typeof( CBase2 ), AutoServiceKind.IsProcessService );
            collector.RegisterType( typeof( CBase1 ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.SimpleMappings.ContainsKey( typeof( CBase1 ) ).Should().BeTrue();
            map.Services.SimpleMappings.ContainsKey( typeof( CBase2 ) ).Should().BeFalse();
        }




    }
}
