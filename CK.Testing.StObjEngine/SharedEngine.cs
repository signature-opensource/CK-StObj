using CK.Core;
using CK.PerfectEvent;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.ExceptionServices;
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
    /// <para>
    /// When <paramref name="reset"/> is true, a new configuration is created (by <see cref="EngineTestHelperExtensions.CreateDefaultEngineConfiguration"/>)
    /// <see cref="AutoConfigure"/> is called on it and any error are cleared.
    /// </para>
    /// </summary>
    /// <param name="reset">True to reset the current configuration if any.</param>
    /// <returns>The engine configuration.</returns>
    public static EngineConfiguration GetEngineConfiguration( bool reset )
    {
        if( reset ) Reset( null );
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
                _runResult = GetEngineConfiguration( false ).RunAsync().Result;
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
                _services = GetMap().CreateAutomaticServices();
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
    /// Called when no configuration exists (or <see cref="Reset(EngineConfiguration?)"/> has been called with null).
    /// </summary>
    public static Action<EngineConfiguration>? AutoConfigure { get; set; }

    /// <summary>
    /// Sets a configuration: this resets existing <see cref="EngineResult"/>, <see cref="Map"/> and <see cref="Services"/>
    /// (as well as any error), the next access to them will trigger a run of the engine.
    /// <para>
    /// When set to null, <see cref="AutoConfigure"/> will be used to obtain a new configuration.
    /// </para>
    /// <para>
    /// The internal configuration is cloned so the <paramref name="engineConfiguration"/> argument can be freely reused
    /// without interfering with the current one. To retrieve the internal configuration, use <see cref="GetEngineConfiguration(bool)"/>.
    /// </para>
    /// <para>
    /// A failing run is not retried, instead the initial exception is rethrown immediately until <see cref="Reset"/>
    /// or <see cref="Reset(EngineConfiguration)"/> is called.
    /// </para>
    /// </summary>
    /// <param name="engineConfiguration">The new configuration to apply.</param>
    public static void Reset( EngineConfiguration? engineConfiguration = null )
    {
        _configuration = engineConfiguration?.Clone();
        _runResult = null;
        _map = null;
        if( _services.Services != null )
        {
            _services.Dispose();
            _services = default;
        }
        _getEngineLevel = _runLevel = _mapLevel = _serviceLevel = null;
    }

    /// <summary>
    /// Gets whether the last run failed. <see cref="Reset"/> must be called to retry.
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
