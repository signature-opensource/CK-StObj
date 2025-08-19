using CK.Engine.TypeCollector;
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

        public bool RegisterHandlerType( IActivityMonitor monitor,
                                         ICachedType type,
                                         [NotNullWhen(true)]out HandlerType? handler )
        {
            if( !_handlers.TryGetValue( type, out handler ) )
            {
                handler = HandlerType.Create( monitor, _loopTree, _parameters, type );
                if( handler == null )
                {
                    return false;
                }
                _handlers.Add( type, handler );
            }
            return true;
        }
    }
}

