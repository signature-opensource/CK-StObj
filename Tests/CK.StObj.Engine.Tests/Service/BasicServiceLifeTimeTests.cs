using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Diagnostics;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class BasicServiceLifetimeTests
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
            var collector = TestHelper.CreateStObjCollector( typeof( SimpleClassSingleton ),
                                                             typeof( SimpleClassScoped ),
                                                             typeof( SimpleClassAmbient ) );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.Mappings[typeof( SimpleClassSingleton )].IsScoped.Should().BeFalse();
            r.Services.Mappings[typeof( SimpleClassScoped )].IsScoped.Should().BeTrue();
            r.Services.Mappings[typeof( SimpleClassAmbient )].IsScoped.Should().BeFalse();
        }

        public class BuggyDoubleScopeClassAmbient : IScopedAutoService, Core.ISingletonAutoService { }

        [Test]
        public void a_class_with_both_scopes_is_an_error()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( BuggyDoubleScopeClassAmbient ) );
            TestHelper.GetFailedResult( collector, "Invalid CK type combination 'IsAutoService|IsScopedService|IsSingleton': An interface or an implementation cannot be both Scoped and Singleton" );
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
            var collector = TestHelper.CreateStObjCollector( typeof( SimpleClassScoped ), 
                                                             typeof( LifetimeErrorClassAmbientBecauseOfScoped ) );
            collector.FatalOrErrors.Count.Should().Be( 0, "Detected by GetResult()." );
            TestHelper.GetFailedResult( collector, "is marked as IsSingleton but parameter 'd' of type 'SimpleClassScoped' in constructor is Scoped." );
        }

        public interface IExternalService { }

        public class LifetimeOfExternalBoostToSingleton : Core.ISingletonAutoService
        {
            public LifetimeOfExternalBoostToSingleton( IExternalService e )
            {
            }
        }

        [Test]
        public void a_singleton_that_depends_on_an_unknown_external_is_not_possible()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( LifetimeOfExternalBoostToSingleton ) );
            collector.FatalOrErrors.Count.Should().Be( 0, "Detected by GetResult()." );
            TestHelper.GetFailedResult( collector, "is marked as IsSingleton but parameter 'e' of type 'IExternalService' in constructor is Scoped." );
        }

        [Test]
        public void a_singleton_that_depends_on_external_that_is_defined_as_a_singleton_is_fine()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( IExternalService ), AutoServiceKind.IsSingleton );
            collector.RegisterType( TestHelper.Monitor, typeof( LifetimeOfExternalBoostToSingleton ) );
            TestHelper.GetSuccessfulResult( collector );
        }

        [Test]
        public void a_singleton_that_depends_on_external_that_is_defined_as_a_Scoped_is_an_error()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( IExternalService ), AutoServiceKind.IsScoped );
            collector.RegisterType( TestHelper.Monitor, typeof( LifetimeOfExternalBoostToSingleton ) );
            TestHelper.GetFailedResult( collector, "s marked as IsSingleton but parameter 'e' of type 'IExternalService' in constructor is Scoped." );
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
            var collector = TestHelper.CreateStObjCollector( typeof( SimpleClassSingleton ),
                                                             typeof( SingletonThatDependsOnSingleton ) );
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
            var collector = TestHelper.CreateStObjCollector( typeof( SimpleClassSingleton ),
                                                             typeof( AmbientThatDependsOnSingleton ) );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.Mappings[typeof( AmbientThatDependsOnSingleton )].IsScoped.Should().BeFalse();
        }

        public interface IAmbientThatDependsOnNothing : IAutoService { }

        public class AmbientThatDependsOnNothing : IAmbientThatDependsOnNothing { }

        [Test]
        public void an_auto_service_that_depends_on_nothing_is_singleton()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( AmbientThatDependsOnNothing ) );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.Mappings[typeof( IAmbientThatDependsOnNothing )].IsScoped.Should().BeFalse();
            r.Services.Mappings[typeof( AmbientThatDependsOnNothing )].IsScoped.Should().BeFalse();
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
            var collector = TestHelper.CreateStObjCollector( typeof( AmbientThatDependsOnExternal ) );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.Mappings[typeof( AmbientThatDependsOnExternal )].IsScoped.Should().BeTrue();
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
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( IExternalService ), AutoServiceKind.IsSingleton );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnAllKindOfSingleton ) );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnExternal ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SampleRealObject ) );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SimpleClassSingleton ) );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatWillBeResolvedAsSingleton ) );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            r.Services.Mappings[typeof( AmbientThatDependsOnAllKindOfSingleton )].IsScoped.Should().BeFalse();
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
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( IExternalService ), AutoServiceKind.IsSingleton );
            if( mode == "WithSingletonLifetimeOnExternalService" )
            {
                collector.SetAutoServiceKind( TestHelper.Monitor, typeof( IOtherExternalService ), AutoServiceKind.IsSingleton );
            }
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService ) );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnExternal ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SampleRealObject ) );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SimpleClassSingleton ) );
            collector.RegisterType( TestHelper.Monitor, typeof( AmbientThatDependsOnAnotherExternalService ) );
            var r = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( r != null, "No initialization error." );
            bool isScoped = r.Services.Mappings[typeof( AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService )].IsScoped;
            isScoped.Should().Be( mode == "UnknwonLifetimeExternalService" );
        }


        public class CBase1 : IAutoService { }

        public class CBase2 : CBase1 { }

        [Test]
        public void SetAutoServiceKind_a_class_does_not_mean_registering_it()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, typeof( CBase1 ), AutoServiceKind.IsScoped );
            collector.RegisterType( TestHelper.Monitor, typeof( CBase1 ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.Mappings.ContainsKey( typeof( CBase1 ) ).Should().BeTrue();
            map.Services.Mappings.ContainsKey( typeof( CBase2 ) ).Should().BeFalse();
        }

        public class OneSingleton : ISingletonAutoService
        {
            public OneSingleton( IUnknown dep ) { }
        }

        public interface IUnknown : IAutoService { }

        public class Unknown : IUnknown
        {
            public Unknown( Scoped s ) { }
        }

        public class Scoped : IScopedAutoService { }

        public class ServiceFreeLifetime : IAutoService
        {
            public ServiceFreeLifetime( IUnknown dep ) { }
        }

        [Test]
        public void propagation_through_an_intermediate_service_1()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( Scoped ), typeof( Unknown ), typeof( OneSingleton ) );

            TestHelper.GetFailedResult( collector, "OneSingleton' is marked as IsSingleton but parameter 'dep' of type 'IUnknown' in constructor is Scoped." );
        }

        [Test]
        public void propagation_through_an_intermediate_service_2()
        {
            var collector = TestHelper.CreateStObjCollector( typeof( Scoped ),
                                                             typeof( Unknown ),
                                                             typeof( ServiceFreeLifetime ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            map.Services.Mappings[typeof( ServiceFreeLifetime )].IsScoped.Should().BeTrue();
        }
    }
}
