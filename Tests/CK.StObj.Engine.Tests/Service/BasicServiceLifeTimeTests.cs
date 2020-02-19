using CK.Core;
using FluentAssertions;
using NUnit.Framework;

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
            var r = TestHelper.GetSuccessfulResult( collector );
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
        public void a_singleton_that_depends_on_an_unknwon_external_defines_the_external_as_singletons()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( LifetimeOfExternalBoostToSingleton ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector );
            r.CKTypeResult.TypeKindDetector.IsSingleton( typeof( IExternalService ) ).Should().BeTrue();
        }

        [Test]
        public void a_singleton_that_depends_on_external_that_is_defined_as_a_singleton_is_fine()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.DefineAsExternalSingletons( new[] { typeof( IExternalService ) } );
            collector.RegisterType( typeof( LifetimeOfExternalBoostToSingleton ) );
            TestHelper.GetSuccessfulResult( collector );
        }

        [Test]
        public void a_singleton_that_depends_on_external_that_is_defined_as_a_Scoped_is_an_error()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.DefineAsExternalScoped( new[] { typeof( IExternalService ) } );
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
            var r = TestHelper.GetSuccessfulResult( collector );
            r.Services.SimpleMappings[typeof( AmbientThatDependsOnSingleton )].IsScoped.Should().BeFalse();
            r.CKTypeResult.TypeKindDetector.IsSingleton( typeof( AmbientThatDependsOnSingleton ) ).Should().BeTrue();
        }

        public interface IAmbientThatDependsOnNothing : IAutoService { }

        public class AmbientThatDependsOnNothing : IAmbientThatDependsOnNothing { }

        [Test]
        public void an_auto_service_that_depends_on_nothing_is_singleton()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( AmbientThatDependsOnNothing ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector );
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
            var r = TestHelper.GetSuccessfulResult( collector );
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
                IAmbientThatDependsOnNothing ambientThatDependsOnNothing,
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
            collector.DefineAsExternalSingletons( new[] { typeof( IExternalService ) } );
            collector.RegisterType( typeof( AmbientThatDependsOnAllKindOfSingleton ) );
            collector.RegisterType( typeof( AmbientThatDependsOnExternal ) );
            collector.RegisterType( typeof( SampleRealObject ) );
            collector.RegisterType( typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( AmbientThatWillBeResolvedAsSingleton ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector );
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
                IAmbientThatDependsOnNothing ambientThatDependsOnNothing,
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
            collector.DefineAsExternalSingletons( new[] { typeof( IExternalService ) } );
            collector.RegisterType( typeof( AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService ) );
            collector.RegisterType( typeof( AmbientThatDependsOnExternal ) );
            collector.RegisterType( typeof( SampleRealObject ) );
            collector.RegisterType( typeof( AmbientThatDependsOnSingleton ) );
            collector.RegisterType( typeof( SimpleClassSingleton ) );
            collector.RegisterType( typeof( AmbientThatDependsOnAnotherExternalService ) );
            if( mode == "WithSingletonLifetimeOnExternalService" )
            {
                collector.DefineAsExternalSingletons( new[] { typeof( IOtherExternalService ) } );
            }
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var r = TestHelper.GetSuccessfulResult( collector );
            bool isScoped = r.Services.SimpleMappings[typeof( AmbientThatDependsOnAllKindOfSingletonAndAnOtherExternalService )].IsScoped;
            isScoped.Should().Be( mode == "UnknwonLifetimeExternalService" );
        }

    }
}
