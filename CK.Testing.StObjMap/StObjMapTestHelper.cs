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
        readonly ITestHelperConfiguration _config;
        readonly IMonitorTestHelper _monitor;
        readonly string _originGeneratedAssemblyName;
        string _generatedAssemblyName;
        static int _resetNumer;

        DateTime _lastLoadedMapUtc;
        DateTime _lastAccessMapUtc;
        IStObjMap _map;
        event EventHandler _stObjMapLoading;
        event EventHandler<StObjMapAccessedEventArgs> _stObjMapAccessed;

        ServiceProvider _automaticServices;
        IStObjMap _automaticServicesSource;
        event EventHandler<AutomaticServicesConfiguredEventArgs> _automaticServicesConfigured;

        /// <summary>
        /// Initializes a new <see cref="StObjMapTestHelper"/>.
        /// </summary>
        /// <param name="config">The configuration.</param>
        /// <param name="monitor">The monitor helper.</param>
        public StObjMapTestHelper( ITestHelperConfiguration config, IMonitorTestHelper monitor )
        {
            _config = config;
            _monitor = monitor;
            _generatedAssemblyName = _originGeneratedAssemblyName = _config.Get( "StObjMap/GeneratedAssemblyName", StObjEngineConfiguration.DefaultGeneratedAssemblyName );
            if( _generatedAssemblyName.IndexOf( ".Reset.", StringComparison.OrdinalIgnoreCase ) >= 0 )
            {
                throw new ArgumentException( "Must not contain '.Reset.' substring.", "StObjMap/GeneratedAssemblyName" );
            }
        }

        IServiceProvider IStObjMapTestHelperCore.AutomaticServices => DoGetAutomaticService();

        IServiceProvider DoGetAutomaticService()
        {
            var current = DoGetStObjMap();
            if( current != _automaticServicesSource )
            {
                if( _automaticServices != null )
                {
                    _automaticServices.Dispose();
                    _automaticServices = null;
                }
                if( current != null )
                {
                    var services = new ServiceCollection();
                    var reg = new StObjContextRoot.ServiceRegister( TestHelper.Monitor, services );
                    reg.AddStObjMap( current );
                    var h = _automaticServicesConfigured;
                    if( h != null )
                    {
                        using( _monitor.Monitor.OpenInfo( "Raising Automatic services configuration event." ) )
                        {
                            h( this, new AutomaticServicesConfiguredEventArgs( current, reg ) );
                        }
                    }
                    _automaticServices = services.BuildServiceProvider();
                }
                _automaticServicesSource = current;
            }
            return _automaticServices;
        }

        event EventHandler<AutomaticServicesConfiguredEventArgs> IStObjMapTestHelperCore.AutomaticServicesConfigured
        {
            add => _automaticServicesConfigured += value;
            remove => _automaticServicesConfigured -= value;
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

        string IStObjMapTestHelperCore.GeneratedAssemblyName => _generatedAssemblyName;

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
                _map = DoLoadStObjMap( _generatedAssemblyName, true );
                if( _map != null ) _lastAccessMapUtc = _lastLoadedMapUtc = DateTime.UtcNow;
            }

            if( _map == null )
            {
                using( _monitor.Monitor.OpenInfo( "Accessing null StObj map." ) )
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
                    if( e.ShouldReload )
                    {
                        using( _monitor.Monitor.OpenInfo( $"Accessing StObj map: current StObjMap should be reloaded." ) )
                        {
                            DoResetStObjMap( true );
                            Load();
                        }
                    }
                    else _lastAccessMapUtc = now;
                }
            }
            return _map;
        }

        IStObjMap IStObjMapTestHelperCore.LoadStObjMap( string assemblyName, bool withWeakAssemblyResolver )
        {
            return DoLoadStObjMap( assemblyName, withWeakAssemblyResolver );
        }

        IStObjMap DoLoadStObjMap( string assemblyName, bool withWeakAssemblyResolver )
        {
            return withWeakAssemblyResolver
                        ? _monitor.WithWeakAssemblyResolver( () => DoLoadStObjMap( assemblyName ) )
                        : DoLoadStObjMap( assemblyName );
        }

        IStObjMap DoLoadStObjMap( string assemblyName )
        {
            using( _monitor.Monitor.OpenInfo( $"Loading StObj map from '{assemblyName}'." ) )
            {
                try
                {
#if NET461
                    var a = Assembly.Load( new AssemblyName( assemblyName ) );
#else
                    var a = Assembly.LoadFrom( Path.Combine( AppContext.BaseDirectory, assemblyName + ".dll" ) );
#endif
                    return StObjContextRoot.Load( a, StObjContextRoot.DefaultStObjRuntimeBuilder, _monitor.Monitor );
                }
                catch( Exception ex )
                {
                    _monitor.Monitor.Error( ex );
                    return null;
                }
            }
        }

        void IStObjMapTestHelperCore.ResetStObjMap( bool deleteGeneratedBinFolderAssembly ) => DoResetStObjMap( deleteGeneratedBinFolderAssembly );

        private void DoResetStObjMap( bool deleteGeneratedBinFolderAssembly )
        {
            if( _map == null ) _monitor.Monitor.Info( $"StObjMap is not loaded yet." );
            _map = null;
            var num = Interlocked.Increment( ref _resetNumer );
            _generatedAssemblyName = $"{_originGeneratedAssemblyName}.Reset.{num}";
            _monitor.Monitor.Info( $"Reseting StObjMap: Generated assembly name is now: {_generatedAssemblyName}." );
            if( deleteGeneratedBinFolderAssembly ) DoDeleteGeneratedAssemblies( _monitor.BinFolder );
        }

        int IStObjMapTestHelperCore.DeleteGeneratedAssemblies( string directory ) => DoDeleteGeneratedAssemblies( directory );

        int DoDeleteGeneratedAssemblies( string directory )
        {
            using( _monitor.Monitor.OpenInfo( $"Deleting generated assemblies from {directory}." ) )
            {
                var r = new Regex( Regex.Escape( _originGeneratedAssemblyName ) + @"(\.Reset\.\d+)?\.dll", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase );
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
