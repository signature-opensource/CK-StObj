using CK.Core;
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
            var c = TestHelper.CreateStObjCollector( typeof( SomeSingletonFailed ), typeof( NotConstructibleServiceNaked ) );
            TestHelper.GetFailedResult( c );
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
            var c1 = TestHelper.CreateStObjCollector( typeof( SomeScoped ), typeof( NotConstructibleServiceNaked ) );
            using var services1 = TestHelper.CreateAutomaticServices( c1 ).Services;
            services1.Invoking( s => s.GetService<SomeScoped>() ).Should().Throw<InvalidOperationException>();

            var c2 = TestHelper.CreateStObjCollector( typeof( SomeScoped ), typeof( NotConstructibleServiceNaked ) );
            using var services2 = TestHelper.CreateAutomaticServices( c2, configureServices: services =>
            {
                services.Services.AddScoped( sp => NotConstructibleServiceNaked.Create() );
            } ).Services;
            services2.GetService<SomeScoped>().Should().NotBeNull();
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
            var c = TestHelper.CreateStObjCollector( typeof( SomeSingleton ), typeof( NotConstructibleService ) );
            using var s = TestHelper.CreateAutomaticServices( c, configureServices: services =>
            {
                services.Services.AddSingleton( sp => NotConstructibleService.Create() );
            } ).Services;
            s.GetService<SomeSingleton>().Should().NotBeNull();
        }

    }
}
