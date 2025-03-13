using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Service;

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
        var map = TestHelper.GetSuccessfulCollectorResult( [typeof( ClassService )] ).EngineMap;
        Throw.DebugAssert( map != null );

        map.Services.Mappings.ContainsKey( typeof( IUsefulService<int> ) ).ShouldBeFalse( "The SuperDefiner." );
        map.Services.Mappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).ShouldBeFalse( "The Definer." );
        map.Services.Mappings[typeof( ClassService )].IsScoped.ShouldBeFalse();
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
    public async Task super_definer_applies_to_final_interface_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ClassFromInterfaceService ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Map.Services.Mappings.ContainsKey( typeof( IUsefulService<int> ) ).ShouldBeFalse( "The SuperDefiner." );
        auto.Map.Services.Mappings.ContainsKey( typeof( IMyServiceTemplate<int> ) ).ShouldBeFalse( "The Definer." );

        auto.Map.Services.Mappings.ContainsKey( typeof( InterfaceService ) ).ShouldBeTrue();
        auto.Map.Services.Mappings[typeof( ClassFromInterfaceService )].UniqueMappings
            .ShouldBe( new[] { typeof( InterfaceService ) } );

        auto.Services.GetService<InterfaceService>().ShouldBe( auto.Services.GetService<ClassFromInterfaceService>() );
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
    public async Task device_host_model_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( ADeviceHost ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        auto.Services.GetService<ADeviceHost>().ShouldNotBeNull();
    }
}
