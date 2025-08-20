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

        public ParameterType? FindParameter( ICachedType type )
        {
            return _parameters.GetValueOrDefault( type );
        }

        public bool RegisterHandlerType( IActivityMonitor monitor,
                                         ReaDIEngine engine,
                                         ICachedType type,
                                         IReaDIHandler handler,
                                         [NotNullWhen( true )] out HandlerType? handlerType )
        {
            if( !_handlers.TryGetValue( type, out handlerType ) )
            {
                handlerType = HandlerType.Create( monitor, engine, _loopTree, _parameters, type, handler );
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

