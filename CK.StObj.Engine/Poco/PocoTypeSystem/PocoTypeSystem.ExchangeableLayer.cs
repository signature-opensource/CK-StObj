using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystem
    {
        // We don't try to be too clever here: we optimise the no change case
        // by reusing the parent map but on restoring we always apply the map.
        // With no changes, memory is good but runs are done (we don't really care
        // given that this should not be used extensively).
        sealed class ExchangeableLayer : IExchangeableLayer
        {
            readonly PocoTypeSystem _typeSystem;
            readonly BitArray _map;
            ExchangeableLayer? _previous;

            public ExchangeableLayer( PocoTypeSystem typeSystem, ExchangeableLayer? previous, bool noChange )
            {
                _typeSystem = typeSystem;
                _previous = previous;
                if( noChange )
                {
                    Throw.DebugAssert( previous != null );
                    _map = previous._map;
                }
                else
                {
                    _map = new BitArray( typeSystem._allTypes.Count );
                    foreach( var t in typeSystem._allTypes )
                    {
                        if( t.IsExchangeable ) _map.Set( t.Index >> 1, true );
                    }
                }
            }

            public IPocoTypeSystem TypeSystem => _typeSystem;

            public bool IsApplied => _previous != null;

            internal void Apply()
            {
                for( int i = 0; i < _map.Length; ++i )
                {
                    _typeSystem._allTypes[i].SetExchangeabilty( _map[i] );
                }
            }

            internal void SetCurrent()
            {
                Throw.DebugAssert( _previous == null );
                _previous = _typeSystem._currentLayer;
                _typeSystem._currentLayer = this;
                Apply();
            }

            internal void OnDispose()
            {
                if( _previous == null ) return;
                var l = _typeSystem._currentLayer;
                while( l != this )
                {
                    Throw.DebugAssert( l != null );
                    var temp = l;
                    l = l._previous;
                    temp._previous = null;
                }
                _previous.Apply();
                _previous = null;
            }

        }

        public IDisposable CreateAndApplyExchangeableLayer( IActivityMonitor monitor, Func<IPocoType, bool> isExchangeable, out IExchangeableLayer layer )
        {
            Throw.DebugAssert( _objectType.Index == 0 && _allTypes[0] == _objectType );
            bool noChange = true;
            for( int i = 1; i < _allTypes.Count; i++ )
            {
                var t = _allTypes[i];
                if( t.IsExchangeable
                    && t.Kind != PocoTypeKind.SecondaryPoco
                    && !isExchangeable( t ) )
                {
                    noChange = false;
                    t.SetNotExchangeable( monitor, "in created ExchangeableLayer." );
                }
            }
            return OnCreate( out layer, noChange );
        }

        public IDisposable CreateAndApplyExchangeableLayer( IActivityMonitor monitor, IEnumerable<IPocoType> notExchangeableTypes, out IExchangeableLayer layer )
        {
            bool noChange = true;
            foreach( var t in notExchangeableTypes )
            {
                if( t.IsExchangeable
                    && t.Kind != PocoTypeKind.SecondaryPoco
                    && t.Kind != PocoTypeKind.Any )
                {
                    noChange = false;
                    ((PocoType)t).SetNotExchangeable( monitor, "in created ExchangeableLayer." );
                }
            }
            return OnCreate( out layer, noChange );
        }

        IDisposable OnCreate( out IExchangeableLayer layer, bool noChange )
        {
            var l = new ExchangeableLayer( this, _currentLayer, noChange );
            layer = l;
            _currentLayer = l;
            return Util.CreateDisposableAction( () => l.OnDispose() );
        }

        public IDisposable ApplyExchangeableLayer( IActivityMonitor monitor, IExchangeableLayer layer )
        {
            Throw.CheckArgument( layer != null && layer.TypeSystem == this );
            if( layer is not ExchangeableLayer l )
            {
                return Throw.ArgumentException<IDisposable>( nameof( layer ), "Invalid implementation type." );
            }
            if( l.IsApplied ) return Util.EmptyDisposable;
            l.SetCurrent();
            return Util.CreateDisposableAction( () => l.OnDispose() );
        }

    }

}
