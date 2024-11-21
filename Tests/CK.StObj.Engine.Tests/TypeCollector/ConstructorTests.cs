using CK.CodeGen;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service.TypeCollector;

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
            collector.RegisterType( TestHelper.Monitor, typeof( ServiceWith2Ctors ) );
            CheckFailure( collector );
        }
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterType( TestHelper.Monitor, typeof( ServiceWithNonPublicCtor ) );
            CheckFailure( collector );
        }
        {
            CheckSuccess( collector =>
            {
                collector.RegisterType( TestHelper.Monitor, typeof( ServiceWithNonPublicCtorButAbstract ) );
            } );
        }
        {
            var r = CheckSuccess( collector =>
            {
                collector.RegisterType( TestHelper.Monitor, typeof( ServiceWithOneCtor ) );
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
                collector.RegisterType( TestHelper.Monitor, typeof( ServiceWithDefaultCtor ) );
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
            collector.RegisterType( TestHelper.Monitor, typeof( AutoRef ) );
            CheckFailure( collector );
        }

        {
            var collector = CreateCKTypeCollector();
            collector.RegisterType( TestHelper.Monitor, typeof( BaseReferencer ) );
            CheckFailure( collector );
        }

        {
            var collector = CreateCKTypeCollector();
            collector.RegisterType( TestHelper.Monitor, typeof( RefIntermediate2 ) );
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
            collector.RegisterType( TestHelper.Monitor, typeof( SpecializedStupidA ) );
            CheckFailure( collector );
        }
    }
}
