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
            var collector = TestHelper.CreateStObjCollector( typeof( ClassService ) );
            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );

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
            var collector = TestHelper.CreateStObjCollector( typeof( ClassFromInterfaceService ) );
            var r = TestHelper.CreateAutomaticServices( collector );
            Debug.Assert( r.CollectorResult.EngineMap != null, "No initialization error." );

            try
            {
                r.CollectorResult.EngineMap.Services.Mappings.ContainsKey( typeof( IUsefulService<int> ) ).Should().BeFalse( "The SuperDefiner." );
                r.CollectorResult.EngineMap.Services.Mappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).Should().BeFalse( "The Definer." );

                r.CollectorResult.EngineMap.Services.Mappings.ContainsKey( typeof( InterfaceService ) ).Should().BeTrue();
                r.CollectorResult.EngineMap.Services.Mappings[typeof( ClassFromInterfaceService )].UniqueMappings.Should().BeEquivalentTo(
                    new[] { typeof( InterfaceService ) } );

                r.Services.GetService<InterfaceService>().Should().Be( r.Services.GetService<ClassFromInterfaceService>() );
            }
            finally
            {
                r.Services.Dispose();
            }
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
            var collector = TestHelper.CreateStObjCollector();
            collector.RegisterType( typeof( ADeviceHost ) );
            using var s = TestHelper.CreateAutomaticServices( collector ).Services;

            s.GetService<ADeviceHost>().Should().NotBeNull(); 
        }
    }
}
