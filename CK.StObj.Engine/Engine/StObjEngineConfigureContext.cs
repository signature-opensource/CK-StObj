using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Setup
{
    sealed class StObjEngineConfigureContext : IStObjEngineConfigureContext
    {
        sealed class Container : SimpleServiceContainer
        {
            readonly SimpleServiceContainer _baseForConfig;
            readonly StObjEngineConfigureContext _c;
            readonly ISimpleObjectActivator _defaultActivator;

            public Container( StObjEngineConfigureContext c )
            {
                _c = c;
                _baseForConfig = new SimpleServiceContainer();
                _defaultActivator = new SimpleObjectActivator();
                _baseForConfig.Add( _defaultActivator );
                BaseProvider = _baseForConfig;
            }

            public void ConfigureDone( IActivityMonitor monitor )
            {
                // Forget the Configure container.
                BaseProvider = null;
                // If the aspects registered a specific object activator, keep it
                // instead of the default one.
                if( GetService( typeof(ISimpleObjectActivator) ) == null )
                {
                    monitor.Info( "No explicit ISimpleObjectActivator has been registered. Using a default SimpleObjectActivator." );
                    this.Add( _defaultActivator );
                }
            }

            protected override object? GetDirectService( Type serviceType )
            {
                object? s = base.GetDirectService( serviceType );
                if( s == null && (serviceType == typeof(IActivityMonitor) || serviceType == typeof(ActivityMonitor)) )
                {
                    s = _c._monitor;
                }
                return s;
            }
        }

        readonly IActivityMonitor _monitor;
        readonly IStObjEngineStatus _status;
        readonly RunningStObjEngineConfiguration _config;
        readonly List<IStObjEngineAspect> _aspects;
        readonly Container _container;
        readonly SimpleServiceContainer _configureOnlycontainer;
        readonly StObjEngineConfigurator _configurator;
        readonly StObjEngineAspectTrampoline<IStObjEngineConfigureContext> _trampoline;

        List<Type>? _explicitRegisteredTypes;
        bool _canSkipRun;

        internal StObjEngineConfigureContext( IActivityMonitor monitor,
                                              RunningStObjEngineConfiguration config,
                                              IStObjEngineStatus status,
                                              bool canSkipRun )
        {
            _monitor = monitor;
            _config = config;
            _status = status;
            _aspects = new List<IStObjEngineAspect>();
            _configurator = new StObjEngineConfigurator();
            _container = new Container( this );
            _configureOnlycontainer = new SimpleServiceContainer( _container );
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineConfigureContext>( this );
            _canSkipRun = canSkipRun;
        }

        public IStObjEngineStatus EngineStatus => _status;

        public void AddExplicitRegisteredType( Type type )
        {
            if( type == null ) throw new ArgumentNullException();
            if( _explicitRegisteredTypes == null ) _explicitRegisteredTypes = new List<Type>();
            _explicitRegisteredTypes.Add( type );
        }

        /// <summary>
        /// Gets or sets whether the run can be sipped.
        /// This can only transition from true to false (setting it to true if it's false has no effect).
        /// <para>
        /// It's initial value is determined by <see cref="StObjEngineConfiguration.ForceRun"/> and by each
        /// <see cref="RunningBinPathGroup.GeneratedAssembly"/> and <see cref="RunningBinPathGroup.GeneratedSource"/>
        /// availability.
        /// </para>
        /// </summary>
        public bool CanSkipRun
        {
            get => _canSkipRun;
            set => _canSkipRun &= value;
        }

        public RunningStObjEngineConfiguration StObjEngineConfiguration => _config;

        IRunningStObjEngineConfiguration IStObjEngineConfigureContext.StObjEngineConfiguration => _config;

        internal IReadOnlyList<Type> ExplicitRegisteredTypes => (IReadOnlyList<Type>?)_explicitRegisteredTypes ?? Type.EmptyTypes;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _aspects;

        public ISimpleServiceContainer ServiceContainer => _container;

        public void AddConfigureOnlyService<T>( ConfigureOnly<T> service ) => _configureOnlycontainer.Add( service );

        public StObjEngineConfigurator Configurator => _configurator;

        public void PushPostConfigureAction( Func<IActivityMonitor, IStObjEngineConfigureContext, bool> postAction ) => _trampoline.Push( postAction );

        internal void CreateAndConfigureAspects( Func<bool> onError )
        {
            bool success = true;
            using( _monitor.OpenTrace( $"Creating and configuring {_config.Configuration.Aspects.Count} aspect(s)." ) )
            {
                var aspectsType = new HashSet<Type>();
                foreach( var c in _config.Configuration.Aspects )
                {
                    if( c == null ) continue;
                    string aspectTypeName = c.AspectType;
                    if( String.IsNullOrWhiteSpace( aspectTypeName ) )
                    {
                        success = onError();
                        _monitor.Error( $"Null or empty {c.GetType()}.AspectType string." );
                    }
                    else
                    {
                        // Registers the configuration instance itself.
                        _container.Add( c.GetType(), c, null );
                        Type? t = SimpleTypeFinder.WeakResolver( aspectTypeName, true );
                        Debug.Assert( t != null );
                        if( !aspectsType.Add( t ) )
                        {
                            success = onError();
                            _monitor.Error( $"Aspect '{t}' occurs more than once in configuration." );
                        }
                        else
                        {
                            IStObjEngineAspect? a = (IStObjEngineAspect?)_configureOnlycontainer.SimpleObjectCreate( _monitor, t );
                            if( a == null ) success = onError();
                            else
                            {
                                _aspects.Add( a );
                                using( _monitor.OpenTrace( $"Configuring aspect '{t}'." ) )
                                {
                                    try
                                    {
                                        if( !a.Configure( _monitor, this ) ) success = onError();
                                    }
                                    catch( Exception ex )
                                    {
                                        success = onError();
                                        _monitor.Error( ex );
                                    }
                                }
                                if( success )
                                {
                                    // Adds the aspect itself to the container.
                                    _container.Add( t, a, null );
                                }
                            }
                        }
                    }
                }
                if( !_trampoline.Execute( _monitor, onError ) ) success = false;
                if( success ) _container.ConfigureDone( _monitor );
            }
        }

        internal void OnSkippedRun( Func<bool> onError )
        {
            foreach( var a in _aspects )
            {
                try
                {
                    if( !a.OnSkippedRun( _monitor ) ) onError();
                }
                catch( Exception ex )
                {
                    _monitor.Error( ex );
                    onError();
                }
            }
        }


    }
}
