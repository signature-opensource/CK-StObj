using CK.Core;
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

            public Container( StObjEngineConfigureContext c )
            {
                _c = c;
                _baseForConfig = new SimpleServiceContainer();
                _baseForConfig.Add<ISimpleObjectActivator>( new SimpleObjectActivator() );
                BaseProvider = _baseForConfig;
            }

            public void ConfigureDone( IActivityMonitor monitor )
            {
                ISimpleObjectActivator defaultActivator = _baseForConfig.GetService<ISimpleObjectActivator>( false );
                BaseProvider = null;
                if( GetService( typeof(ISimpleObjectActivator) ) == null )
                {
                    monitor.Info( "No explicit ISimpleObjectActivator has been registered. Using a default SimpleObjectActivator." );
                    this.Add( defaultActivator );
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

        internal StObjEngineConfigureContext( IActivityMonitor monitor, RunningStObjEngineConfiguration config, IStObjEngineStatus status )
        {
            _monitor = monitor;
            _config = config;
            _status = status;
            _aspects = new List<IStObjEngineAspect>();
            _configurator = new StObjEngineConfigurator();
            _container = new Container( this );
            _configureOnlycontainer = new SimpleServiceContainer( _container );
            _trampoline = new StObjEngineAspectTrampoline<IStObjEngineConfigureContext>( this );
        }

        public IStObjEngineStatus EngineStatus => _status;

        public void AddExplicitRegisteredType( Type type )
        {
            if( type == null ) throw new ArgumentNullException();
            if( _explicitRegisteredTypes == null ) _explicitRegisteredTypes = new List<Type>();
            _explicitRegisteredTypes.Add( type );
        }

        public RunningStObjEngineConfiguration StObjEngineConfiguration => _config;

        IRunningStObjEngineConfiguration IStObjEngineConfigureContext.StObjEngineConfiguration => _config;

        internal IReadOnlyList<Type> ExplicitRegisteredTypes => (IReadOnlyList<Type>?)_explicitRegisteredTypes ?? Type.EmptyTypes;

        public IReadOnlyList<IStObjEngineAspect> Aspects => _aspects;

        public ISimpleServiceContainer ServiceContainer => _container;

        public void AddConfigureOnlyService<T>( ConfigureOnly<T> service ) => _configureOnlycontainer.Add( service );

        public StObjEngineConfigurator Configurator => _configurator;

        public void PushPostConfigureAction( Func<IActivityMonitor, IStObjEngineConfigureContext, bool> postAction ) => _trampoline.Push( postAction );

        internal void CreateAspects( Func<bool> onError )
        {
            bool success = true;
            using( _monitor.OpenTrace( $"Creating {_config.Configuration.Aspects.Count} aspect(s)." ) )
            {
                var aspectsType = new HashSet<Type>();
                foreach( var c in _config.Configuration.Aspects )
                {
                    if( c == null ) continue;
                    string aspectTypeName = c.AspectType;
                    if( String.IsNullOrWhiteSpace( aspectTypeName ) )
                    {
                        success = onError();
                        _monitor.Error( $"Null or empty {c.GetType().FullName}.AspectType string." );
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
                            _monitor.Error( $"Aspect '{t.FullName}' occurs more than once in configuration." );
                        }
                        else
                        {
                            IStObjEngineAspect? a = (IStObjEngineAspect?)_configureOnlycontainer.SimpleObjectCreate( _monitor, t );
                            if( a == null ) success = onError();
                            else
                            {
                                _aspects.Add( a );
                                // Adds the aspect itself to the container.
                                _container.Add( t, a, null );
                            }
                        }
                    }
                }
            }
        }

        internal void ConfigureAspects( Func<bool> onError )
        {
            bool success = true;
            using( _monitor.OpenTrace( $"Configuring {_aspects.Count} aspect(s)." ) )
            {
                foreach( var a in _aspects )
                {
                    using( _monitor.OpenTrace( $"Configuring aspect '{a}'." ) )
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
                }
                if( !_trampoline.Execute( _monitor, onError ) ) success = false;
                if( success ) _container.ConfigureDone( _monitor );
            }
        }


    }
}
