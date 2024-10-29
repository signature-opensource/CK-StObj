using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup;

sealed class AspectInitializer : IEngineAspectInitializationContext
{
    readonly SimpleServiceContainer _container;
    readonly StObjEngineConfigurator _configurator;
    readonly List<EngineAspect> _aspects;
    readonly AspectTrampolineImpl _reusableTrampoline;
    SimpleServiceContainer? _configOnlyContainer;

    AspectInitializer( StObjEngineConfigurator realObjectConfigurator )
    {
        _container = new SimpleServiceContainer();
        _configurator = realObjectConfigurator;
        _aspects = new List<EngineAspect>();
    }

    ISimpleServiceContainer IEngineAspectInitializationContext.ServiceContainer => _container;

    StObjEngineConfigurator IEngineAspectInitializationContext.Configurator => _configurator;

    IReadOnlyList<EngineAspect> IEngineAspectInitializationContext.Aspects => _aspects;

    void IEngineAspectInitializationContext.AddConfigureOnlyService<T>( T service ) where T : class
    {
        if( _configOnlyContainer == null )
        {
            _configOnlyContainer = new SimpleServiceContainer();
            _container.BaseProvider = _configOnlyContainer;
        }
        _configOnlyContainer.Add( typeof(T), service );
    }

    void IEngineAspectInitializationContext.PushDeferredAction( Func<IActivityMonitor, bool> postAction )
    {
        _reusableTrampoline.Push( postAction );
    }

    bool Run( IActivityMonitor monitor, EngineConfiguration configuration )
    {
        bool success = true;
        // Registers the Engine configuration instance itself.
        Throw.DebugAssert( typeof( EngineConfiguration ).IsSealed );
        _container.Add( typeof( EngineConfiguration ), configuration, null );
        // Temporarily register the monitor.
        _container.Add( typeof(IActivityMonitor), monitor );
        using var errorTracker = monitor.OnError( () => success = false );
        errorTracker.Enabled = false;
        var aspectsType = new HashSet<Type>();
        foreach( var c in configuration.Aspects )
        {
            if( c == null ) continue;
            string aspectTypeName = c.AspectType;
            if( String.IsNullOrWhiteSpace( aspectTypeName ) )
            {
                monitor.Error( $"Null or empty {c:N}.AspectType string." );
                success = false;
            }
            else
            {
                // We ensure that the EngineAspectConfiguration is sealed here to prevent any type mismatch.
                var cType = c.GetType();
                if( !cType.IsSealed )
                {
                    monitor.Error( $"Aspect '{c.AspectName}': configuration type '{cType:N}' must be sealed." );
                    success = false;
                }
                else
                {
                    // Registers the aspect configuration instance itself: this allows ActivatorUtilities
                    // to be able to create the instance without explicit parameters
                    // (and other aspects to inject other configurations even if it is a bit useless
                    // since the Aspect exposes its configuration).
                    _container.Add( cType, c, null );
                    Type? t = SimpleTypeFinder.WeakResolver( aspectTypeName, true );
                    Throw.DebugAssert( t != null );
                    if( !aspectsType.Add( t ) )
                    {
                        monitor.Error( $"Aspect '{c.AspectName}' occurs more than once in configuration." );
                        success = false;
                    }
                    else
                    {
                        errorTracker.Enabled = true;
                        EngineAspect? a = TryCreate( monitor, _container, t );
                        if( a == null ) return false;
                        errorTracker.Enabled = false;
                        _aspects.Add( a );
                        // Adds the aspect itself to the container (even if the initialization fails)
                        // to allow dependent aspect to be satisfied if possible.
                        _container.Add( t, a, null );
                        using( monitor.OpenTrace( $"Initializing aspect '{c.AspectName}'." ) )
                        {
                            try
                            {
                                if( !a.Initialize( monitor, this ) )
                                {
                                    monitor.CloseGroup( "Failed." );
                                    success = false;
                                }
                            }
                            catch( Exception ex )
                            {
                                monitor.Error( ex );
                                monitor.CloseGroup( "Failed." );
                                success = false;
                            }
                        }
                    }
                }
            }
        }
        if( success )
        {
            success = _reusableTrampoline.Execute( monitor );
        }
        _container.Remove( typeof( IActivityMonitor ) );
        _container.BaseProvider = null;
        return success;

        static EngineAspect? TryCreate( IActivityMonitor monitor, IServiceProvider services, Type t )
        {
            try
            {
                return (EngineAspect)ActivatorUtilities.CreateInstance( services, t );
            }
            catch( Exception ex )
            {
                monitor.Error( $"Error while instanciating '{t:N}'.", ex );
                return null;
            }
        }
    }

    public static bool CreateAndInitializeAspects( IActivityMonitor monitor,
                                                  EngineConfiguration configuration,
                                                  StObjEngineConfigurator realObjectConfigurator,
                                                  [NotNullWhen(true)] out SimpleServiceContainer? container,
                                                  [NotNullWhen( true )] out IReadOnlyList<EngineAspect>? aspects )
    {
        container = null;
        aspects = null;
        using( monitor.OpenTrace( $"Creating and configuring {configuration.Aspects.Count} aspect(s)." ) )
        {
            var initializer = new AspectInitializer( realObjectConfigurator );
            var success = initializer.Run( monitor, configuration );
            if( success )
            {
                container = initializer._container;
                aspects = initializer._aspects;
            }
            else
            {
                monitor.Trace( $"Aspects initialization failed. Normalized EngineConfiguration is:{Environment.NewLine}{configuration.ToXml()}" );
            }
            return success;
        }
    }
}
