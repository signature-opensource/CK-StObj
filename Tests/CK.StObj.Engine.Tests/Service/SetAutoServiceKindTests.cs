using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using static CK.StObj.Engine.Tests.Service.MultipleServiceTests;
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

        [Test]
        public void simple_front_only_registration()
        {
            var config = TestHelper.CreateDefaultEngineConfiguration();
            config.FirstBinPath.Types.Add( new BinPathConfiguration.TypeConfiguration( typeof( IService ), AutoServiceKind.IsScoped | AutoServiceKind.IsMultipleService ) );

            var collector = TestHelper.CreateTypeCollector( typeof( TheService ) );

            var result = TestHelper.RunSingleBinPathAndLoad( config, collector );

            var d = result.Map.Services.Mappings[typeof( TheService )];
            d.AutoServiceKind.Should().Be( AutoServiceKind.IsAutoService | AutoServiceKind.IsScoped );
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
            var collector = new Setup.StObjCollector();
            collector.SetAutoServiceKind( TestHelper.Monitor, "CK.StObj.Engine.Tests.Service.SetAutoServiceKindTests+OpenGeneric`1, CK.StObj.Engine.Tests", AutoServiceKind.IsSingleton, true );
            collector.RegisterType( TestHelper.Monitor, typeof( GenService ) );
            var r = collector.GetResult( TestHelper.Monitor );
            r.HasFatalError.Should().BeFalse();
        }


        public interface IConfiguration { }

        public interface IConfigurationSection : IConfiguration { }

        public class ThisIsTheConfig : IConfiguration { }

        public class ThisShouldCoexist1 : IConfigurationSection { }
        class ThisShouldCoexist2 : IConfigurationSection { }

        [Test]
        public void base_singleton_interface_definition_can_coexist_with_specializations()
        {
            var config = TestHelper.CreateDefaultEngineConfiguration();
            config.FirstBinPath.Types.Add( new BinPathConfiguration.TypeConfiguration( typeof( IConfiguration ), AutoServiceKind.IsSingleton ) );

            var collector = TestHelper.CreateTypeCollector( typeof( ThisIsTheConfig ), typeof( ThisShouldCoexist1 ), typeof( ThisShouldCoexist2 ) );

            // TestHelper.GetFailedAutomaticServicesConfiguration( collector );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( config, collector, configureServices: register =>
            {
                // This is done by .net configuration extension.
                register.Services.AddSingleton<IConfiguration>( new ThisIsTheConfig() );
            } );

            auto.Services.GetService<IConfiguration>( throwOnNull: true ).Should().BeOfType<ThisIsTheConfig>();
            auto.Services.GetService<IConfigurationSection>( throwOnNull: false ).Should().BeNull();
        }

    }
}
