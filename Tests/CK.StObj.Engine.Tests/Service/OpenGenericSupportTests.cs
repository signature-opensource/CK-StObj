using CK.Core;
using CK.Testing;
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
    public class OpenGenericSupportTests
    {

        [CKTypeSuperDefiner]
        public interface IUsefulService<out T> : IAutoService
        {
            T Value { get; }
        }

        public interface IMyServiceTemplate<T> : IUsefulService<T>
        {
            void SetValue( T v );
        }

        public class ClassService : IMyServiceTemplate<int>
        {
            public int Value => 5;

            public void SetValue( int v ) { }
        }


        [Test]
        public void super_definer_applies_to_final_class()
        {
            var collector = TestHelper.CreateTypeCollector( typeof( ClassService ) );
            var map = TestHelper.GetSuccessfulCollectorResult( collector ).EngineMap;
            Throw.DebugAssert( map != null );

            map.Services.Mappings.ContainsKey( typeof( IUsefulService<int> ) ).Should().BeFalse( "The SuperDefiner." );
            map.Services.Mappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).Should().BeFalse( "The Definer." );
            map.Services.Mappings[typeof( ClassService )].IsScoped.Should().BeFalse();
        }

        public interface InterfaceService : IMyServiceTemplate<int>
        {
        }


        public class ClassFromInterfaceService : InterfaceService
        {
            public int Value => 5;

            public void SetValue( int v ) { }
        }

        [Test]
        public void super_definer_applies_to_final_interface()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Add( typeof( ClassFromInterfaceService ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            auto.Map.Services.Mappings.ContainsKey( typeof( IUsefulService<int> ) ).Should().BeFalse( "The SuperDefiner." );
            auto.Map.Services.Mappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).Should().BeFalse( "The Definer." );

            auto.Map.Services.Mappings.ContainsKey( typeof( InterfaceService ) ).Should().BeTrue();
            auto.Map.Services.Mappings[typeof( ClassFromInterfaceService )].UniqueMappings
                .Should().BeEquivalentTo( new[] { typeof( InterfaceService ) } );

            auto.Services.GetService<InterfaceService>().Should().Be( auto.Services.GetService<ClassFromInterfaceService>() );
        }

        [IsMultiple]
        public interface IDeviceHost : ISingletonAutoService
        {
        }

        interface IInternalDeviceHost : IDeviceHost { }


        [CKTypeDefiner]
        public abstract class DeviceHost<T1, T2> : IInternalDeviceHost
        {
        }

        public class ADeviceHost : DeviceHost<int, string>
        {
        }

        [Test]
        public void device_host_model()
        {
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Add( typeof( ADeviceHost ) );
            using var auto = configuration.Run().CreateAutomaticServices();

            auto.Services.GetService<ADeviceHost>().Should().NotBeNull(); 
        }
    }
}
