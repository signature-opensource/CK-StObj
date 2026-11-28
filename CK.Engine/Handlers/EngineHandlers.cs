using CK.Core;
using CK.TypeCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// The engine handlers, in their call order: the core handlers first (the legacy bridge is the first one), then the
/// <see cref="EngineAttributeImpl"/> of the types of the Apps (ordered by type name, then in the order of their
/// declarations).
/// </summary>
sealed class EngineHandlers
{
    readonly struct Handler
    {
        public Handler( object instance, HandlerType type, ICachedType? item )
        {
            Instance = instance;
            Type = type;
            Item = item;
        }

        public object Instance { get; }

        public HandlerType Type { get; }

        /// <summary>
        /// The type that is decorated by an <see cref="EngineAttributeImpl"/>: its App methods are called only
        /// for the Apps that contain it. Null for a core handler.
        /// </summary>
        public ICachedType? Item { get; }
    }

    readonly List<Handler> _handlers;

    EngineHandlers( List<Handler> handlers )
    {
        _handlers = handlers;
    }

    /// <summary>
    /// Creates the handlers. Errors are logged.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="gen">The root context.</param>
    /// <param name="coreHandlers">The handlers of the engine itself.</param>
    /// <returns>The handlers or null on error.</returns>
    internal static EngineHandlers? Create( IActivityMonitor monitor, GenContext gen, IEnumerable<object> coreHandlers )
    {
        var types = new Dictionary<Type, HandlerType?>();
        var handlers = new List<Handler>();
        bool success = true;
        foreach( var h in coreHandlers )
        {
            Add( h, null );
        }
        // The type sets are HashSets: the types are ordered so that the generated code doesn't depend on hash codes.
        var allTypes = gen.Apps.SelectMany( a => a.Group.ConfiguredTypes.AllTypes )
                               .Distinct()
                               .OrderBy( t => t.CSharpName, StringComparer.Ordinal )
                               .ThenBy( t => t.Type.Assembly.FullName, StringComparer.Ordinal );
        foreach( var t in allTypes )
        {
            if( !t.TryGetAllAttributes( monitor, out var attributes ) )
            {
                success = false;
                continue;
            }
            foreach( var impl in attributes.OfType<EngineAttributeImpl>() )
            {
                Add( impl, t );
            }
        }
        if( !success ) return null;
        monitor.Info( $"{handlers.Count} engine handler(s)." );
        return new EngineHandlers( handlers );

        void Add( object h, ICachedType? item )
        {
            var t = h.GetType();
            if( !types.TryGetValue( t, out var type ) )
            {
                type = HandlerType.Create( monitor, t );
                types.Add( t, type );
            }
            if( type == null ) success = false;
            else if( !type.IsEmpty ) handlers.Add( new Handler( h, type, item ) );
        }
    }

    /// <summary>
    /// Calls the methods of a slot. All of them are called, even if one of them fails.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="slot">The slot.</param>
    /// <param name="gen">The root context.</param>
    /// <param name="app">The App context for an App slot.</param>
    /// <returns>False if an error has been logged.</returns>
    internal bool Call( IActivityMonitor monitor, HandlerSlot slot, GenContext gen, AppGenContext? app )
    {
        Throw.DebugAssert( (app != null) == (slot >= HandlerSlot.StartingApp) );
        bool success = true;
        foreach( var h in _handlers )
        {
            var methods = h.Type[slot];
            if( methods.Length == 0 ) continue;
            if( app != null && h.Item != null && !app.Group.ConfiguredTypes.AllTypes.Contains( h.Item ) ) continue;
            foreach( var m in methods )
            {
                using( monitor.OnError( () => success = false ) )
                {
                    try
                    {
                        m.Info.Invoke( h.Instance, m.CreateArguments( monitor, gen, app ) );
                    }
                    catch( TargetInvocationException ex )
                    {
                        monitor.Error( $"While calling '{m}' ({slot}).", ex.InnerException ?? ex );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( $"While calling '{m}' ({slot}).", ex );
                    }
                }
            }
        }
        return success;
    }
}
