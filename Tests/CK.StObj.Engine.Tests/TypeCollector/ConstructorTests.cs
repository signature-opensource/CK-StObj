using CK.CodeGen;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;

namespace CK.StObj.Engine.Tests.Service.TypeCollector
{
    [TestFixture]
    public class ConstructorTests : TypeCollectorTestsBase
    {

        // This fails.
        public class ServiceWith2Ctors : IScopedAutoService
        {
            public ServiceWith2Ctors()
            {
            }

            public ServiceWith2Ctors( int a )
            {
            }
        }


        public class ServiceWithOneCtor : IScopedAutoService
        {
            public ServiceWithOneCtor( int a )
            {
            }
        }

        public class ServiceWithNonPublicCtor : IScopedAutoService
        {
            internal ServiceWithNonPublicCtor( int a )
            {
            }
        }

        public abstract class ServiceWithNonPublicCtorButAbstract : IScopedAutoService
        {
            protected ServiceWithNonPublicCtorButAbstract( int a )
            {
            }
        }

        public class ServiceWithDefaultCtor : IScopedAutoService
        {
        }

        [Test]
        public void services_must_have_only_one_public_ctor_or_no_constructor_at_all()
        {
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( ServiceWith2Ctors ) );
                CheckFailure( collector );
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( ServiceWithNonPublicCtor ) );
                CheckFailure( collector );
            }
            {
                CheckSuccess( collector =>
                {
                    collector.RegisterType( typeof( ServiceWithNonPublicCtorButAbstract ) );
                } );
            }
            {
                var r = CheckSuccess( collector =>
                {
                    collector.RegisterType( typeof( ServiceWithOneCtor ) );
                } );
                var c = r.AutoServices.RootClasses.Single( x => x.ClassType == typeof( ServiceWithOneCtor ) );
                Debug.Assert( c.ConstructorParameters != null );
                c.ConstructorParameters.Should().HaveCount( 1 );
                c.ConstructorParameters[0].IsAutoService.Should().BeFalse();
                c.ConstructorParameters[0].Name.Should().Be( "a" );
            }
            {
                var r = CheckSuccess( collector =>
                {
                    collector.RegisterType( typeof( ServiceWithDefaultCtor ) );
                } );
                var c = r.AutoServices.RootClasses.Single( x => x.ClassType == typeof( ServiceWithDefaultCtor ) );
                c.ConstructorParameters.Should().BeEmpty();
            }
        }

        public interface INotAnAutoService { }

        public interface ISNotRegistered : IScopedAutoService { }

        public interface ISRegistered : IScopedAutoService { }

        public class ServiceForISRegistered : ISRegistered { }

        public class Consumer1Service : IScopedAutoService
        {
            public Consumer1Service(
                INotAnAutoService normal,
                ISNotRegistered notReg,
                ISRegistered reg )
            {
            }
        }

        [TestCase( "RegisteredDependentServiceButExcluded" )]
        [TestCase( "RegisteredDependentService" )]
        [TestCase( "NotRegistered" )]
        public void ctor_parameters_can_be_unregistered_services_interfaces_since_they_may_be_registered_at_runtime( string mode )
        {
            var r = CheckSuccess( collector =>
            {
                if( mode != "NotRegistered" ) collector.RegisterClass( typeof( ServiceForISRegistered ) );
                collector.RegisterClass( typeof( Consumer1Service ) );
            }, mode == "RegisteredDependentServiceButExcluded"
                            ? CreateCKTypeCollector( t => t != typeof( ServiceForISRegistered ) )
                            : CreateCKTypeCollector() );

            var iRegistered = r.AutoServices.LeafInterfaces.SingleOrDefault( x => x.Type == typeof( ISRegistered ) );
            if( mode == "RegisteredDependentService" )
            {
                iRegistered.Should().NotBeNull();
            }
            r.AutoServices.RootClasses.Should().HaveCount( mode == "RegisteredDependentService" ? 2 : 1 );
            var c = r.AutoServices.RootClasses.Single( x => x.ClassType == typeof( Consumer1Service ) );
            c.ConstructorParameters.Should().HaveCount( 3 );
            Debug.Assert( c.ConstructorParameters != null );
            c.ConstructorParameters[0].Name.Should().Be( "normal" );
            c.ConstructorParameters[1].Name.Should().Be( "notReg" );
            c.ConstructorParameters[2].Name.Should().Be( "reg" );
            if( mode == "RegisteredDependentService" )
            {
                c.ConstructorParameters[0].IsAutoService.Should().BeFalse();

                c.ConstructorParameters[1].IsAutoService.Should().BeFalse();

                c.ConstructorParameters[2].IsAutoService.Should().BeTrue();
                c.ConstructorParameters[2].ServiceClass.Should().BeNull();
                c.ConstructorParameters[2].ServiceInterface.Should().BeSameAs( iRegistered );
            }
            else
            {
                c.ConstructorParameters[0].IsAutoService.Should().BeFalse();
                c.ConstructorParameters[1].IsAutoService.Should().BeFalse();
                c.ConstructorParameters[2].IsAutoService.Should().BeFalse();
            }
        }

        public class ConsumerWithClassDependencyService : IScopedAutoService
        {
            public ConsumerWithClassDependencyService(
                INotAnAutoService normal,
                ISNotRegistered notReg,
                ServiceForISRegistered classDependency )
            {
            }
        }

        public class ConsumerWithDefaultService : IScopedAutoService
        {
            public ConsumerWithDefaultService(
                INotAnAutoService normal,
                ISNotRegistered notReg,
                ServiceForISRegistered? classDependency = null )
            {
            }
        }

        [Test]
        public void ctor_parameters_cannot_be_unregistered_service_classe_unless_it_is_excluded_and_parameter_has_a_default_null()
        {
            {
                var r = CheckSuccess( collector =>
                {
                    collector.RegisterClass( typeof( ServiceForISRegistered ) );
                    collector.RegisterClass( typeof( ConsumerWithClassDependencyService ) );
                } );
                var dep = r.AutoServices.RootClasses.Single( x => x.ClassType == typeof( ServiceForISRegistered ) );
                var c = r.AutoServices.RootClasses.Single( x => x.ClassType == typeof( ConsumerWithClassDependencyService ) );
                c.ConstructorParameters.Should().HaveCount( 3 );
                Debug.Assert( c.ConstructorParameters != null );
                c.ConstructorParameters[2].ParameterType.Should().Be( typeof( ServiceForISRegistered ) );
                c.ConstructorParameters[2].Position.Should().Be( 2 );
                c.ConstructorParameters[2].Name.Should().Be( "classDependency" );
                c.ConstructorParameters[2].ServiceClass.Should().BeSameAs( dep );
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterClass( typeof( ConsumerWithClassDependencyService ) );
                CheckFailure( collector );
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterClass( typeof( ConsumerWithDefaultService ) );
                CheckFailure( collector );
            }
            {
                var r = CheckSuccess( collector =>
                {
                    collector.RegisterClass( typeof( ServiceForISRegistered ) );
                    collector.RegisterClass( typeof( ConsumerWithDefaultService ) );
                }, CreateCKTypeCollector( t => t != typeof( ServiceForISRegistered ) ) );
                r.AutoServices.RootClasses.Should().HaveCount( 1 );
                var c = r.AutoServices.RootClasses.Single( x => x.ClassType == typeof( ConsumerWithDefaultService ) );
                c.ConstructorParameters.Should().HaveCount( 3 );
            }

        }

        public class AutoRef : IScopedAutoService
        {
            public AutoRef( AutoRef a )
            {
            }
        }

        public class RefBased : IScopedAutoService
        {
        }

        public class BaseReferencer : RefBased
        {
            public BaseReferencer( RefBased b )
            {
            }
        }

        public class RefIntermediate : RefBased { }

        public class RefIntermediate2 : RefIntermediate
        {
            public RefIntermediate2( RefBased b )
            {
            }
        }


        [Test]
        public void no_constructor_parameter_super_type_rule()
        {
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( AutoRef ) );
                CheckFailure( collector );
            }

            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( BaseReferencer ) );
                CheckFailure( collector );
            }

            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( RefIntermediate2 ) );
                CheckFailure( collector );
            }

        }

        public class StupidA : IScopedAutoService
        {
            public StupidA( SpecializedStupidA? child )
            {
            }
        }

        public class SpecializedStupidA : StupidA
        {
            public SpecializedStupidA()
                : base( null )
            {
            }
        }

        [Test]
        public void stupid_loop()
        {
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( SpecializedStupidA ) );
                CheckFailure( collector );
            }
        }
    }
}
