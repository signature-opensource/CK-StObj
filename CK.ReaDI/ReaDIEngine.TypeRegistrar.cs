using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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

        public ParameterType? FindParameter( ICachedType type )
        {
            return _parameters.GetValueOrDefault( type );
        }

        public bool RegisterHandlerType( IActivityMonitor monitor,
                                         ICachedType type,
                                         IReaDIHandler handler,
                                         [NotNullWhen(true)]out HandlerType? handlerType )
        {
            if( !_handlers.TryGetValue( type, out handlerType ) )
            {
                handlerType = HandlerType.Create( monitor, _loopTree, _parameters, type, handler );
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

