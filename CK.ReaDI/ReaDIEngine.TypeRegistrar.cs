using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.AccessControl;

namespace CK.Core;

public sealed partial class ReaDIEngine
{

    sealed class TypeRegistrar
    {
        readonly Dictionary<ICachedType, HandlerType> _handlerTypes;
        readonly Dictionary<ICachedType, ParameterType> _parameters;
        readonly LoopTree _loopTree;

        public TypeRegistrar( GlobalTypeCache typeCache )
        {
            _handlerTypes = new Dictionary<ICachedType, HandlerType>();
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
                foreach( var handler in _handlerTypes.Values )
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

        internal Dictionary<ICachedType, HandlerType> Handlers => _handlerTypes;

        internal IEnumerable<KeyValuePair<ICachedType, object>> HandlersAsWaitingObjects
        {
            get
            {
                foreach( var h in _handlerTypes.Values )
                {
                    var c = h.CurrentHandler;
                    if( c != null )
                    {
                        yield return KeyValuePair.Create( h.Type, (object)c );
                    }
                }
            }
        }

        public bool RegisterHandlerTypeFromObject( IActivityMonitor monitor,
                                                   ReaDIEngine engine,
                                                   ICachedType type,
                                                   IReaDIHandler handler,
                                                   [NotNullWhen( true )] out HandlerType? handlerType )
        {
            if( _handlerTypes.TryGetValue( type, out handlerType ) )
            {
                if( handlerType.IsFromSourceType )
                {
                    monitor.Error( $"ReaDIHandler '{type}' registered from engine attributes (at least from type '{handlerType.InitialSourceType}') cannot be explicitly added." );
                    return false;
                }
                return true;
            }
            handlerType = HandlerType.Create( monitor, engine, _loopTree, _parameters, type, sourceType: null, handler );
            if( handlerType == null )
            {
                return false;
            }
            _handlerTypes.Add( type, handlerType );
            return true;
        }

        public bool RegisterHandlerTypeForSourceType( IActivityMonitor monitor,
                                                      ReaDIEngine engine,
                                                      ICachedType type,
                                                      SourcedType sourcedType,
                                                      IReaDIHandler handler )
        {
            if( _handlerTypes.TryGetValue( type, out var handlerType ) )
            {
                if( !handlerType.IsFromSourceType )
                {
                    monitor.Error( $"A ReaDIHandler '{type}' instance registered from engine attributes (from type '{sourcedType}') has been previously explicitly added." );
                    return false;
                }
                handlerType.AddSourceInstance( sourcedType, handler );
                return true;
            }

            handlerType = HandlerType.Create( monitor, engine, _loopTree, _parameters, type, sourcedType, handler );
            if( handlerType == null )
            {
                return false;
            }
            Throw.DebugAssert( handlerType.FirstSourcedHandler != null );
            _handlerTypes.Add( type, handlerType );
            return true;
        }
    }
}

