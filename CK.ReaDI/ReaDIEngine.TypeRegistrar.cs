using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class TypeRegistrar
    {
        readonly Dictionary<ICachedType, HandlerType> _handlers;
        readonly Dictionary<ICachedType, ParameterType> _parameters;
        readonly LoopTree _loopTree;

        public TypeRegistrar( GlobalTypeCache typeCache )
        {
            _handlers = new Dictionary<ICachedType, HandlerType>();
            _parameters = new Dictionary<ICachedType, ParameterType>();
            _loopTree = new LoopTree( typeCache );
        }

        internal bool CheckIntrinsicType( IActivityMonitor monitor, ICachedType oT )
        {
            if( oT.Interfaces.Contains( _loopTree.IActivityMonitorType )
                || oT == _loopTree.ReaDIEngineType )
            {
                monitor.Error( $"Invalid added object '{oT}'. This type is a ReaDIEngine intrinsic type and must not be explicitly added." );
                return false;
            }
            return true;
        }

        public IEnumerable<Callable> AllCallables
        {
            get
            {
                foreach( var handler in _handlers.Values )
                {
                    var c = handler.FirstCallable;
                    while( c != null )
                    {
                        yield return c;
                        c = c._next;
                    }
                }
            }
        }

        internal Dictionary<ICachedType, ParameterType> ParameterTypes => _parameters;

        internal Dictionary<ICachedType, HandlerType> Handlers => _handlers;

        internal IEnumerable<KeyValuePair<ICachedType, object>> HandlersAsWaitingObjects
        {
            get
            {
                foreach( var h in _handlers.Values )
                {
                    if( h.CurrentHandler != null )
                    {
                        yield return KeyValuePair.Create( h.Type, (object)h.CurrentHandler );
                    }
                }
            }
        }

        public bool RegisterHandlerType( IActivityMonitor monitor,
                                         ReaDIEngine engine,
                                         ICachedType type,
                                         IReaDIHandler handler,
                                         [NotNullWhen( true )] out HandlerType? handlerType )
        {
            if( !_handlers.TryGetValue( type, out handlerType ) )
            {
                var baseHandler = type.BaseType != null && type.Interfaces.Contains( _loopTree.IReaDIHandler )
                                    ? type.BaseType
                                    : null;
                HandlerType? baseHandlerType = null;
                if( baseHandler != null
                    && !RegisterHandlerType( monitor, engine, baseHandler, handler, out baseHandlerType ) )
                {
                    return false;
                }
                handlerType = HandlerType.Create( monitor, engine, _loopTree, baseHandlerType, _parameters, type, handler );
                if( handlerType == null )
                {
                    return false;
                }
                _handlers.Add( type, handlerType );
            }
            return true;
        }
    }
}

