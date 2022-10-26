using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service
{
    public class SetAutoServiceKindTests
    {
        public interface IService
        {
        }

        public class TheService : IService, IAutoService
        {
        }

        [TestCase( true )]
        [TestCase( false )]
        public void simple_front_only_registration( bool isOptional )
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+IService, CK.StObj.Engine.Tests", AutoServiceKind.IsScoped | AutoServiceKind.IsMultipleService, isOptional );
            collector.RegisterType( TestHelper.Monitor, typeof( TheService ) );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

            var d = map.Services.SimpleMappings[typeof( TheService )];
            d.AutoServiceKind.Should().Be( AutoServiceKind.IsScoped );
            d.MultipleMappings.Should().Contain( typeof( IService ) );
        }

        // This is defined as a Singleton:
        // SetAutoServiceKind( "...+OpenGeneric`1, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, true );
        public class OpenGeneric<T> where T : struct { public T MagicValue; }

        public class GenService : ISingletonAutoService
        {
            public GenService( OpenGeneric<int> dep )
            {
            }
        }

        [Test]
        public void late_resolving_open_generics()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+OpenGeneric`1, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, true );
            collector.RegisterType( TestHelper.Monitor, typeof( GenService ) );
            TestHelper.GetSuccessfulResult( collector );
        }


        public interface IConfiguration { }

        public interface IConfigurationSection : IConfiguration { }

        public class ThisIsTheConfig : IConfiguration { }

        public class ThisShouldCoexist1 : IConfigurationSection { }
        class ThisShouldCoexist2 : IConfigurationSection { }

        [Test]
        public void base_singleton_interface_definition_can_coexist_with_specializations()
        {
            var collector = TestHelper.CreateStObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+IConfiguration, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, false );

            collector.RegisterTypes( TestHelper.Monitor, new[] { typeof( ThisIsTheConfig ), typeof( ThisShouldCoexist1 ), typeof( ThisShouldCoexist2 ) } );

            // TestHelper.GetFailedAutomaticServicesConfiguration( collector );
            using var services = TestHelper.CreateAutomaticServices( collector, configureServices: register =>
            {
                // This is done by .net configuration extension.
                register.Services.AddSingleton<IConfiguration>( new ThisIsTheConfig() );
            } ).Services;

            services.GetService<IConfiguration>( throwOnNull: true ).Should().BeOfType<ThisIsTheConfig>();
            services.GetService<IConfigurationSection>( throwOnNull: false ).Should().BeNull();
        }

    }
}
