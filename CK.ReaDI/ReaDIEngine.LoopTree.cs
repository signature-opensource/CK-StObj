using CK.Engine.TypeCollector;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class LoopTree
    {
        readonly GlobalTypeCache _typeCache;
        readonly ICachedType _voidType;
        readonly ICachedType _iActivityMonitorType;
        readonly ICachedType _reaDIEngineType;
        readonly ICachedType _iReaDIHandler;
        LoopParameterType? _firstChild;

        public LoopTree( GlobalTypeCache typeCache )
        {
            _typeCache = typeCache;
            _voidType = typeCache.KnownTypes.Void;
            _iActivityMonitorType = typeCache.KnownTypes.IActivityMonitor;
            _reaDIEngineType = typeCache.Get( typeof( ReaDIEngine ) );
            _iReaDIHandler = typeCache.Get( typeof( IReaDIHandler ) );
        }

        public GlobalTypeCache TypeCache => _typeCache;

        public ICachedType VoidType => _voidType;

        public ICachedType IActivityMonitorType => _iActivityMonitorType;

        public ICachedType ReaDIEngineType => _reaDIEngineType;

        public ICachedType IReaDIHandler => _iReaDIHandler;

        internal LoopParameterType? FindOrCreateFromNewParameter( IActivityMonitor monitor, ParameterType p, ICachedType loopStateType )
        {
            var result = _firstChild != null ? FindByType( _firstChild, p.Type ) : null;
            result ??= Create( monitor, p.Type, creator: p );
            if( result != null )
            {
                Throw.DebugAssert( "Just created or created via a child.", !result.HasParameter );
                Throw.DebugAssert( "It cannot have a loop state.", result.LoopStateType == _voidType );
                result.SetFirstParameter( p, loopStateType );
            }
            return result;
        }

        internal bool TryFindOrCreateFromHandlerType( IActivityMonitor monitor, ICachedType type, out LoopParameterType? loopParameter )
        {
            loopParameter = _firstChild != null ? FindByType( _firstChild, type ) : null;
            if( loopParameter == null )
            {
                if( type.IsHierarchicalType )
                {
                    loopParameter = Create( monitor, type, creator: null );
                    if( loopParameter == null )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        static LoopParameterType? FindByType( [DisallowNull]LoopParameterType? first, ICachedType type )
        {
            while( first != null )
            {
                if( first.Type == type ) return first;
                var c = first._firstChild;
                if( c != null )
                {
                    c = FindByType( c, type );
                    if( c != null ) return c;
                }
                first = first._next;
            }
            return null;
        }

        LoopParameterType? Create( IActivityMonitor monitor, ICachedType type, object? creator )
        {
            if( !type.IsHierarchicalType )
            {
                if( creator is ParameterType p )
                {
                    monitor.Error( $"Type '{type}' must be decorated with [HierarchicalTypeRoot] or [HierarchicalType<>] because " +
                                   $"it is referenced by parameter {p}." );
                }
                else
                {
                    Throw.DebugAssert( creator is ICachedType );
                    monitor.Error( $"Type '{type}' must be decorated with [HierarchicalTypeRoot] or [HierarchicalType<>] because " +
                                   $"it is referenced by [ReaDILoopParameter<{type.Name}>] of {creator}." );
                }
                return null;
            }
            if( type.IsHierarchicalTypeRoot )
            {
                var newRoot = new LoopParameterType( this, type, parent: null );
                newRoot._next = _firstChild;
                _firstChild = newRoot;
                return newRoot;
            }
            var tParent = type.HierarchicalTypePath[^2];
            var nParent = _firstChild != null ? FindByType( _firstChild, tParent ) : null;
            nParent ??= Create( monitor, tParent, creator: type );
            return nParent == null
                    ? null
                    : new LoopParameterType( this, type, nParent );
        }

    }
}

