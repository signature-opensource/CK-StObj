using CK.Core;
using CK.PerfectEvent;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Testing;

/// <summary>
/// Basic helper that works on a shared engine configuration and keeps
/// a long lived CKomposable map and automatic services.
/// <para>
/// The basic usage is simply to get the <see cref="AutomaticServices"/> property (or the <see cref="Map"/>).
/// </para>
/// <para>
/// The <see cref="AutoConfigure"/> can be used to alter a brand new configuration: this should be
/// used from <see cref="System.Runtime.CompilerServices.ModuleInitializerAttribute"/>.
/// </para>
/// </summary>
public static class SharedEngine
{
    static IStObjMap? _map;
    static AutomaticServices _services;
    static EngineConfiguration? _configuration;
    static ExceptionDispatchInfo? _getEngineLevel;
    static ExceptionDispatchInfo? _runLevel;
    static ExceptionDispatchInfo? _mapLevel;
    static ExceptionDispatchInfo? _serviceLevel;
    static EngineResult? _runResult;

    /// <summary>
    /// Gets the engine configuration that has been used (or will be used) by <see cref="EngineResult"/>, <see cref="Map"/> or <see cref="AutomaticServices"/>.
    /// </summary>
    /// <returns>The engine configuration.</returns>
    public static EngineConfiguration GetEngineConfiguration()
    {
        if( _getEngineLevel != null ) _getEngineLevel.Throw();
        try
        {
            if( _configuration == null )
            {
                _configuration = TestHelper.CreateDefaultEngineConfiguration();
                AutoConfigure?.Invoke( _configuration );
            }
            return _configuration;
        }
        catch( Exception ex )
        {
            _getEngineLevel = ExceptionDispatchInfo.Capture( ex );
            throw;
        }
    }

    static EngineResult GetResult()
    {
        if( _runLevel != null ) _runLevel.Throw();
        try
        {
            if( _runResult == null )
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                _runResult = GetEngineConfiguration().RunAsync().GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            }
            return _runResult;
        }
        catch( Exception ex )
        {
            _runLevel = ExceptionDispatchInfo.Capture( ex );
            throw;
        }
    }

    static IStObjMap GetMap()
    {
        if( _mapLevel != null ) _mapLevel.Throw();
        try
        {
            if( _map == null )
            {
                _map = GetResult().LoadMap();
            }
            return _map;
        }
        catch( Exception ex )
        {
            _mapLevel = ExceptionDispatchInfo.Capture( ex );
            throw;
        }
    }

    static IServiceProvider GetServices()
    {
        if( _serviceLevel != null ) _serviceLevel.Throw();
        try
        {
            if( _services.Services == null )
            {
                _services = GetMap().CreateAutomaticServices( AutoConfigureServices );
            }
            return _services.Services;
        }
        catch( Exception ex )
        {
            _serviceLevel = ExceptionDispatchInfo.Capture( ex );
            throw;
        }
    }

    /// <summary>
    /// Called when no configuration exists (or <see cref="ResetAsync(EngineConfiguration?)"/> has been called with null).
    /// </summary>
    public static Action<EngineConfiguration>? AutoConfigure { get; set; }

    /// <summary>
    /// Called by <see cref="AutomaticServices"/> when services must be built.
    /// </summary>
    public static Action<IServiceCollection>? AutoConfigureServices { get; set; }

    /// <summary>
    /// Sets a configuration: this resets existing <see cref="EngineResult"/>, <see cref="Map"/> and <see cref="AutomaticServices"/>
    /// (as well as any error), the next access to them will trigger a run of the engine.
    /// <para>
    /// When <paramref name="engineConfiguration"/> is null, <see cref="AutoConfigure"/> will be used to setup a new configuration.
    /// </para>
    /// <para>
    /// The internal configuration is cloned so the <paramref name="engineConfiguration"/> argument can be freely reused
    /// without interfering with the current one. To retrieve the internal configuration, use <see cref="GetEngineConfiguration"/>.
    /// </para>
    /// <para>
    /// A failing run is not retried, instead the initial exception is rethrown immediately until <see cref="ResetAsync(EngineConfiguration?)"/>
    /// is called.
    /// </para>
    /// </summary>
    /// <param name="engineConfiguration">The new configuration to apply.</param>
    /// <returns>The awaitable.</returns>
    public static async Task ResetAsync( EngineConfiguration? engineConfiguration = null )
    {
        _configuration = engineConfiguration?.Clone();
        _runResult = null;
        _getEngineLevel = _runLevel = null;
        await ResetMapAsync();
    }

    /// <summary>
    /// Resets the <see cref="Map"/> and the <see cref="AutomaticServices"/>: The next call to <see cref="Map"/> (or <see cref="AutomaticServices"/>)
    /// will reload the map.
    /// The current <see cref="EngineResult"/> (if any) is preserved.
    /// </summary>
    /// <returns>The awaitable.</returns>
    public static async Task ResetMapAsync()
    {
        _map = null;
        _mapLevel = null;
        await ResetAutomaticServicesAsync();
    }

    /// <summary>
    /// Resets the <see cref="AutomaticServices"/>: The next call to <see cref="AutomaticServices"/> 
    /// will recreate a configured <see cref="AutomaticServices"/>.
    /// The current <see cref="EngineResult"/> and <see cref="Map"/> (if any) are preserved.
    /// </summary>
    /// <returns>The awaitable.</returns>
    public static async Task ResetAutomaticServicesAsync()
    {
        _serviceLevel = null;
        if( _services.Services != null )
        {
            await _services.DisposeAsync();
            _services = default;
        }
    }

    /// <summary>
    /// Gets whether the last run failed. One of the ResetAsync methods must be called to retry.
    /// </summary>
    public static bool HasError => _runLevel != null || _mapLevel != null || _serviceLevel != null;

    /// <summary>
    /// Gets the <see cref="EngineResult"/> (that may be <see cref="RunStatus.Failed"/>).
    /// </summary>
    public static EngineResult EngineResult => GetResult();

    /// <summary>
    /// Gets the CKomposable map.
    /// </summary>
    public static IStObjMap Map => GetMap();

    /// <summary>
    /// Gets the fully configured root services provider.
    /// <para>
    /// Unless you work only with singletons, you SHOULD work in a <see cref="IServiceScope"/>
    /// or <see cref="AsyncServiceScope"/>: use <see cref="ServiceProviderServiceExtensions.CreateScope(IServiceProvider)"/>
    /// in a using statement.
    /// </para>
    /// </summary>
    public static IServiceProvider AutomaticServices => GetServices();
}
