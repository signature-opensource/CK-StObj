using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    [TestFixture]
    public class SingletonAttributeTests
    {
        [SingletonService]
        public class NotConstructibleService
        {
            private NotConstructibleService() { }

            public static NotConstructibleService Create() => new NotConstructibleService();
        }

        public class NotConstructibleServiceNaked
        {
            private NotConstructibleServiceNaked() { }

            public static NotConstructibleServiceNaked Create() => new NotConstructibleServiceNaked();
        }

        public class SomeSingletonFailed : ISingletonAutoService
        {
            public SomeSingletonFailed( NotConstructibleServiceNaked c )
            {
            }
        }

        [Test]
        public void without_SingletonServiceAttribute_scope_lifetime_fails()
        {
            var c = TestHelper.CreateTypeCollector( typeof( SomeSingletonFailed ), typeof( NotConstructibleServiceNaked ) );
            TestHelper.GetFailedCollectorResult( c, "is marked as IsSingleton but parameter 'c' of type 'NotConstructibleServiceNaked' in constructor is Scoped" );
        }

        public class SomeScoped : IScopedAutoService
        {
            public SomeScoped( NotConstructibleServiceNaked c )
            {
            }
        }

        [Test]
        public void without_SingletonServiceAttribute_scope_requires_manual_registration()
        {
            var c1 = TestHelper.CreateTypeCollector( typeof( SomeScoped ), typeof( NotConstructibleServiceNaked ) );
            using var auto1 = TestHelper.CreateSingleBinPathAutomaticServices( c1 );
            auto1.Services.Invoking( s => s.GetService<SomeScoped>() ).Should().Throw<InvalidOperationException>();

            var c2 = TestHelper.CreateTypeCollector( typeof( SomeScoped ), typeof( NotConstructibleServiceNaked ) );
            using var auto2 = TestHelper.CreateSingleBinPathAutomaticServices( c2, configureServices: services =>
            {
                services.Services.AddScoped( sp => NotConstructibleServiceNaked.Create() );
            } );
            auto2.Services.GetService<SomeScoped>().Should().NotBeNull();
        }

        public class SomeSingleton : ISingletonAutoService
        {
            public SomeSingleton( NotConstructibleService c )
            {
            }
        }


        [Test]
        public void SingletonServiceAttribute_enables_services_to_not_have_public_constructor()
        {
            var c = TestHelper.CreateTypeCollector( typeof( SomeSingleton ), typeof( NotConstructibleService ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddSingleton( sp => NotConstructibleService.Create() );
            } );
            auto.Services.GetService<SomeSingleton>().Should().NotBeNull();
        }

    }
}
