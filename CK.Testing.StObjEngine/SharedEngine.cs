using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Runtime.ExceptionServices;
using static CK.Testing.MonitorTestHelper;

namespace CK.Testing
{
    /// <summary>
    /// Basic helper that works on a shared engine configurationand keeps
    /// a long lived CKomposable map and automatic services.
    /// </summary>
    public static class SharedEngine
    {
        static IStObjMap? _map;
        static AutomaticServices _services;
        static EngineConfiguration _configuration = TestHelper.CreateDefaultEngineConfiguration();
        static ExceptionDispatchInfo? _mapLevel;
        static ExceptionDispatchInfo? _serviceLevel;

        static IStObjMap GetMap()
        {
            if( _mapLevel != null ) _mapLevel.Throw();
            try
            {
                if( _map == null )
                {
                    _map = _configuration.Run().LoadMap();
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
        /// Sets a configuration: this resets existing <see cref="Map"/> and <see cref="Services"/>, the next
        /// access to them will trigger a run of the engine.
        /// <para>
        /// The internal configuration is cloned so the <paramref name="engineConfiguration"/> argument can be freely reused
        /// without interfering with the current one.
        /// </para>
        /// <para>
        /// A failing run is not retried, instead the initial exception is rethrown immediately until <see cref="ResetError"/>
        /// or <see cref="SetEngineConfiguration(EngineConfiguration)"/> is called.
        /// </para>
        /// </summary>
        /// <param name="engineConfiguration">The new configuration to apply.</param>
        public static void SetEngineConfiguration( EngineConfiguration engineConfiguration ) 
        {
            Throw.CheckNotNullArgument( engineConfiguration );
            _configuration = engineConfiguration.Clone();
            _map = null;
            if( _services.Services != null )
            {
                _services.Dispose();
                _services = default;
                ResetError();
            }
        }

        /// <summary>
        /// Gets whether the last run failed. <see cref="ResetError"/> must be called to retry.
        /// </summary>
        public static bool OnError => _mapLevel != null || _serviceLevel != null;

        /// <summary>
        /// Resets any error. An access to the <see cref="Map"/> or the <see cref="Services"/>
        /// will run the engine.
        /// </summary>
        public static void ResetError() => _mapLevel = _serviceLevel = null;

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
}