using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using CK.Core;
using CK.Setup;
using CK.Testing.StObjMap;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Testing
{

    /// <summary>
    /// Provides default implementation of <see cref="IStObjMapTestHelperCore"/>.
    /// </summary>
    public class StObjMapTestHelper : IStObjMapTestHelperCore
    {
        const string _binPathNamePrefix = "StObjMapTest";
        readonly TestHelperConfiguration _config;
        readonly IMonitorTestHelper _monitor;
        string _binPathName;
        bool _stObjMapRetryOnError;
        bool _lastStObjMapLoadFailed; 
        static int _resetNumer;

        DateTime _lastLoadedMapUtc;
        DateTime _lastAccessMapUtc;
        IStObjMap? _map;
        event EventHandler? _stObjMapLoading;
        event EventHandler<StObjMapAccessedEventArgs>? _stObjMapAccessed;

        ServiceProvider? _automaticServices;
        IStObjMap? _automaticServicesSource;
        event EventHandler<AutomaticServicesConfigurationEventArgs>? _automaticServicesConfiguring;
        event EventHandler<AutomaticServicesConfigurationEventArgs>? _automaticServicesConfigured;

        /// <summary>
        /// Initializes a new <see cref="StObjMapTestHelper"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="monitor">The monitor helper.</param>
        public StObjMapTestHelper( TestHelperConfiguration config, IMonitorTestHelper monitor )
        {
            _config = config;
            _monitor = monitor;
            _binPathName = _binPathNamePrefix;
            _stObjMapRetryOnError = _config.DeclareBoolean( "StObjMap/StObjMapRetryOnError",
                                                            false,
                                                            "By default if the first attempt to obtain the current StObjMap failed, subsequent attempts immediately throw. Set it to true to always retry.",
                                                            () => _stObjMapRetryOnError.ToString() ).Value;
        }

        IServiceProvider IStObjMapTestHelperCore.AutomaticServices => DoGetAutomaticService( null );

        ServiceProvider IStObjMapTestHelperCore.CreateAutomaticServices( SimpleServiceContainer? startupServices ) => DoCreateAutomaticServices( startupServices, DoGetStObjMap() );

        IServiceProvider DoGetAutomaticService( SimpleServiceContainer? startupServices )
        {
            var current = DoGetStObjMap();
            if( current != _automaticServicesSource )
            {
                if( _automaticServices != null )
                {
                    _automaticServices.Dispose();
                    _automaticServices = null;
                }
                _automaticServices = DoCreateAutomaticServices( startupServices, current );
                _automaticServicesSource = current;
            }
            return _automaticServices!;
        }

        ServiceProvider DoCreateAutomaticServices( SimpleServiceContainer? startupServices, IStObjMap current )
        {
            var services = new ServiceCollection();
            var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, services, startupServices );

            var configureArgs = new AutomaticServicesConfigurationEventArgs( current, reg );
            var hIng = _automaticServicesConfiguring;
            if( hIng != null )
            {
                using( _monitor.Monitor.OpenInfo( "Raising Automatic services configuring event." ) )
                {
                    hIng( this, configureArgs );
                }
            }
            if( !reg.AddStObjMap( current ) ) Throw.Exception( "AddStObjMap failed. The logs contains detailed information." );
            var hEd = _automaticServicesConfigured;
            if( hEd != null )
            {
                using( _monitor.Monitor.OpenInfo( "Raising Automatic services configured event." ) )
                {
                    hEd( this, configureArgs );
                }
            }
            return services.BuildServiceProvider();
        }

        string IStObjMapTestHelperCore.BinPathName => _binPathName;

        string GetDllName() => $"{EngineConfiguration.GeneratedAssemblyNamePrefix}.{_binPathName}.dll";

        event EventHandler<AutomaticServicesConfigurationEventArgs> IStObjMapTestHelperCore.AutomaticServicesConfigured
        {
            add => _automaticServicesConfigured += value;
            remove => _automaticServicesConfigured -= value;
        }

        event EventHandler<AutomaticServicesConfigurationEventArgs> IStObjMapTestHelperCore.AutomaticServicesConfiguring
        {
            add => _automaticServicesConfiguring += value;
            remove => _automaticServicesConfiguring -= value;
        }

        event EventHandler IStObjMapTestHelperCore.StObjMapLoading
        {
            add => _stObjMapLoading += value;
            remove => _stObjMapLoading -= value;
        }

        event EventHandler<StObjMapAccessedEventArgs> IStObjMapTestHelperCore.StObjMapAccessed
        {
            add => _stObjMapAccessed += value;
            remove => _stObjMapAccessed -= value;
        }

        bool IStObjMapTestHelperCore.StObjMapRetryOnError
        {
            get => _stObjMapRetryOnError;
            set => _stObjMapRetryOnError = value;
        }

        IStObjMap IStObjMapTestHelperCore.StObjMap => DoGetStObjMap();

        IStObjMap DoGetStObjMap()
        {
            void Load()
            {
                var h = _stObjMapLoading;
                if( h != null )
                {
                    using( _monitor.Monitor.OpenInfo( "Invoking StObjMapLoading event." ) )
                    {
                        h( this, EventArgs.Empty );
                    }
                }
                _lastStObjMapLoadFailed = false;
                _map = DoLoadStObjMap( true );
                if( _map == null )
                {
                    _lastStObjMapLoadFailed = true;
                    Throw.Exception( "Unable to load StObjMap. See logs for details." );
                }
                _lastAccessMapUtc = _lastLoadedMapUtc = DateTime.UtcNow;
            }

            if( _map == null )
            {
                if( _lastStObjMapLoadFailed && !_stObjMapRetryOnError )
                {
                    Throw.Exception( "Previous attempt to load the StObj map failed and StObjMapRetryOnError is false." );
                }
                var msg = "Accessing null StObj map.";
                var current = GetDllName();
                var currentPath = AppContext.BaseDirectory + '\\' + current;
                bool currentExists = File.Exists( currentPath );
                if( currentExists )
                {
                    msg += $" The assembly '{current}' exists.";
                }
                if( _lastStObjMapLoadFailed ) msg += " (Previous attempt to load it failed but retrying since StObjMapRetryOnError is true.)";
                using( _monitor.Monitor.OpenInfo( msg ) )
                {
                    Load();
                }
            }
            else
            {
                var h = _stObjMapAccessed;
                if( h != null )
                {
                    var now = DateTime.UtcNow;
                    var e = new StObjMapAccessedEventArgs( _map, now - _lastAccessMapUtc, now - _lastLoadedMapUtc );
                    h( this, e );
                    if( e.ShouldReset )
                    {
                        using( _monitor.Monitor.OpenInfo( $"Accessing StObj map: current StObjMap should be reset." ) )
                        {
                            DoResetStObjMap( true );
                            Load();
                        }
                    }
                    else _lastAccessMapUtc = now;
                }
            }
            return _map!;
        }

        IStObjMap? DoLoadStObjMap( bool withWeakAssemblyResolver )
        {
            string dllName = GetDllName();
            return withWeakAssemblyResolver
                        ? _monitor.WithWeakAssemblyResolver( () => StObjContextRoot.Load( dllName, _monitor.Monitor ) )
                        : StObjContextRoot.Load( dllName, _monitor.Monitor );
        }

        void IStObjMapTestHelperCore.ResetStObjMap( bool deleteGeneratedBinFolderAssembly ) => DoResetStObjMap( deleteGeneratedBinFolderAssembly );

        void DoResetStObjMap( bool deleteGeneratedBinFolderAssembly )
        {
            if( _map == null ) _monitor.Monitor.Info( $"StObjMap is not loaded yet." );
            _map = null;
            var num = ++_resetNumer;
            _binPathName = $"{IStObjMapTestHelperCore.BinPathNamePrefix}{num}";
            _monitor.Monitor.Info( $"Reseting StObjMap: Generated assembly must now be: '{GetDllName()}'." );
            if( deleteGeneratedBinFolderAssembly ) DoDeleteGeneratedAssemblies( _monitor.BinFolder );
            _lastStObjMapLoadFailed = false;
        }

        int IStObjMapTestHelperCore.DeleteGeneratedAssemblies( string directory ) => DoDeleteGeneratedAssemblies( directory );

        int DoDeleteGeneratedAssemblies( string directory )
        {
            using( _monitor.Monitor.OpenInfo( $"Deleting generated assemblies from '{directory}'." ) )
            {
                var r = new Regex( Regex.Escape( EngineConfiguration.GeneratedAssemblyNamePrefix ) + '.' + IStObjMapTestHelperCore.BinPathNamePrefix + @"\d*?\.dll", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase );
                int count = 0;
                if( Directory.Exists( directory ) )
                {
                    foreach( var f in Directory.EnumerateFiles( directory ) )
                    {
                        if( r.IsMatch( f ) )
                        {
                            _monitor.Monitor.Info( $"Deleting Generated assembly: {f}." );
                            try
                            {
                                File.Delete( f );
                            }
                            catch( Exception ex )
                            {
                                _monitor.Monitor.Error( ex );
                            }
                            ++count;
                        }
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// Gets the <see cref="IStObjMapTestHelper"/> default implementation.
        /// </summary>
        public static IStObjMapTestHelper TestHelper => TestHelperResolver.Default.Resolve<IStObjMapTestHelper>();

    }
}
