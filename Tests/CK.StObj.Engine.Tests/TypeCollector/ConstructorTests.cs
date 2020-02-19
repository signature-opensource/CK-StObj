using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System.Linq;

namespace CK.StObj.Engine.Tests.Service.TypeCollector
{
    [TestFixture]
    public class ConstructorTests : TypeCollectorTestsBase
    {
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class PackageA : IRealObject
        {
        }

        //[AutoService( typeof( PackageA ) )]
        public class ServiceWith2Ctors : IScopedAutoService
        {
            public ServiceWith2Ctors()
            {
            }

            public ServiceWith2Ctors( int a )
            {
            }
        }


        //[AutoService( typeof( PackageA ) )]
        public class ServiceWithOneCtor : IScopedAutoService
        {
            public ServiceWithOneCtor( int a )
            {
            }
        }

        //[AutoService( typeof( PackageA ) )]
        public class ServiceWithNonPublicCtor : IScopedAutoService
        {
            internal ServiceWithNonPublicCtor( int a )
            {
            }
        }

        //[AutoService( typeof( PackageA ) )]
        public class ServiceWithDefaultCtor : IScopedAutoService
        {
        }

        [Test]
        public void services_must_have_one_and_only_one_public_ctor()
        {
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( PackageA ) );
                collector.RegisterType( typeof( ServiceWith2Ctors ) );
                CheckFailure( collector );
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( PackageA ) );
                collector.RegisterType( typeof( ServiceWithNonPublicCtor ) );
                CheckFailure( collector );
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( PackageA ) );
                collector.RegisterType( typeof( ServiceWithOneCtor ) );
                var r = CheckSuccess( collector );
                var c = r.AutoServices.RootClasses.Single( x => x.Type == typeof( ServiceWithOneCtor ) );
                c.ConstructorInfo.Should().NotBeNull();
                var p = c.ConstructorParameters.Should().BeEmpty();
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterType( typeof( PackageA ) );
                collector.RegisterType( typeof( ServiceWithDefaultCtor ) );
                var r = CheckSuccess( collector );
                var c = r.AutoServices.RootClasses.Single( x => x.Type == typeof( ServiceWithDefaultCtor ) );
                c.ConstructorInfo.Should().NotBeNull();
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
            var collector = mode == "RegisteredDependentServiceButExcluded"
                            ? CreateCKTypeCollector( t => t != typeof( ServiceForISRegistered ) )
                            : CreateCKTypeCollector();

            if( mode != "NotRegistered" ) collector.RegisterClass( typeof( ServiceForISRegistered ) );
            collector.RegisterClass( typeof( Consumer1Service ) );
            var r = CheckSuccess( collector );
            var iRegistered = r.AutoServices.LeafInterfaces.SingleOrDefault( x => x.Type == typeof( ISRegistered ) );
            if( mode == "RegisteredDependentService" )
            {
                iRegistered.Should().NotBeNull();
            }
            r.AutoServices.RootClasses.Should().HaveCount( mode == "RegisteredDependentService" ? 2 : 1 );
            var c = r.AutoServices.RootClasses.Single( x => x.Type == typeof( Consumer1Service ) );
            c.ConstructorInfo.Should().NotBeNull();
            if( mode == "RegisteredDependentService" )
            {
                c.ConstructorParameters.Should().HaveCount( 1 );
                c.ConstructorParameters[0].ParameterInfo.Name.Should().Be( "reg" );
                c.ConstructorParameters[0].ServiceClass.Should().BeNull();
                c.ConstructorParameters[0].ServiceInterface.Should().BeSameAs( iRegistered );
            }
            else
            {
                c.ConstructorParameters.Should().BeEmpty();
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
                ServiceForISRegistered classDependency = null )
            {
            }
        }

        [Test]
        public void ctor_parameters_cannot_be_unregistered_service_classe_unless_it_is_excluded_and_parameter_has_a_default_null()
        {
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterClass( typeof( ServiceForISRegistered ) );
                collector.RegisterClass( typeof( ConsumerWithClassDependencyService ) );
                var r = CheckSuccess( collector );
                var dep = r.AutoServices.RootClasses.Single( x => x.Type == typeof( ServiceForISRegistered ) );
                var c = r.AutoServices.RootClasses.Single( x => x.Type == typeof( ConsumerWithClassDependencyService ) );
                c.ConstructorParameters.Should().HaveCount( 1, "'INotAnAutoService normal' and 'ISNotRegistered notReg' are ignored." );
                c.ConstructorParameters[0].Position.Should().Be( 2 );
                c.ConstructorParameters[0].Name.Should().Be( "classDependency" );
                c.ConstructorParameters[0].ServiceClass.Should().BeSameAs( dep );
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
                var collector = CreateCKTypeCollector( t => t != typeof( ServiceForISRegistered ) );
                collector.RegisterClass( typeof( ServiceForISRegistered ) );
                collector.RegisterClass( typeof( ConsumerWithDefaultService ) );
                var r = CheckSuccess( collector );
                r.AutoServices.RootClasses.Should().HaveCount( 1 );
                var c = r.AutoServices.RootClasses.Single( x => x.Type == typeof( ConsumerWithDefaultService ) );
                c.ConstructorParameters.Should().BeEmpty();
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

        class BaseReferencer : RefBased
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
            public StupidA( SpecializedStupidA child )
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
