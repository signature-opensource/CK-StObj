using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    readonly GlobalTypeCache _typeCache;
    readonly Dictionary<ICachedType, ParameterType> _reaDIParameters;
    readonly Dictionary<object, ICachedType> _objects;
    readonly LoopContext _rootContext;
    // Using a FIFO offers determinism.
    readonly Queue<OldCallable> _oldReadyToRun;
    readonly Queue<Callable> _readyToRun;
    LoopContext _currentContext;
    ParameterType? _firstActiveDescriptor;
    bool _hasError;

    public ReaDIEngine( GlobalTypeCache typeCache )
    {
        _typeCache = typeCache;
        _reaDIParameters = new Dictionary<ICachedType, ParameterType>();
        _objects = new Dictionary<object, ICachedType>();
        _rootContext = new LoopContext( this, null );
        _currentContext = _rootContext;
        _oldReadyToRun = new Queue<OldCallable>( 128 );
        _readyToRun = new Queue<Callable>( 128 );
    }

    public IReaDIContext Context => _currentContext;

    bool SetError( IActivityMonitor monitor )
    {
        if( !_hasError )
        {
            _hasError = true;
            monitor.Error( $"ReaDIEngine is on error." );
        }
        return false;
    }

    bool AddObject( IActivityMonitor monitor, object o, [NotNullWhen(true)]out ICachedType? oT )
    {
        if( _objects.TryGetValue( o, out oT ) )
        {
            monitor.Error( $"Duplicate object of type '{oT}' registration." );
            return false;
        }
        oT = _typeCache.Get( o.GetType() );
        // TODO: check type before returning.
        _objects.Add( o, oT );
        return true;
    }

    bool RemoveHandler( IActivityMonitor monitor, IReaDIHandler h )
    {
        if( !_hosts.TryGetValue( h, out var host ) )
        {
            monitor.Error( $"Unable to remove unregistered IReaDIHandler of type '{h.GetType():N}'." );
            return false;
        }
        if( host.Loop == null )
        {
            monitor.Error( $"Unable to remove IReaDIHandler '{h.GetType():N}'. It has not been registred as a loop handler." );
            return false;
        }
        host.Remove( this );
        _hosts.Remove( h );
        return true;
    }

    void Deactivate( ParameterType d )
    {
        if( d._prevActiveDescriptor == null )
        {
            Throw.DebugAssert( _firstActiveDescriptor == d );
            if( d._nextActiveDescriptor == null )
            {
                _firstActiveDescriptor = null;
            }
            else
            {
                _firstActiveDescriptor = d._nextActiveDescriptor;
                _firstActiveDescriptor._prevActiveDescriptor = null;
            }
        }
        else
        {
            Throw.DebugAssert( _firstActiveDescriptor != null && _firstActiveDescriptor != d );
            Throw.DebugAssert( d._prevActiveDescriptor._nextActiveDescriptor == d
                               && (d._nextActiveDescriptor == null || d._nextActiveDescriptor._prevActiveDescriptor == d) );
            d._prevActiveDescriptor._nextActiveDescriptor = d._nextActiveDescriptor;
            if( d._nextActiveDescriptor != null )
            {
                d._nextActiveDescriptor._prevActiveDescriptor = d._prevActiveDescriptor;
            }
        }
        d._prevActiveDescriptor = null;
        d._nextActiveDescriptor = null;
    }

    void Activate( ParameterType d )
    {
        Throw.DebugAssert( d._nextActiveDescriptor == null && d._prevActiveDescriptor == null );
        if( _firstActiveDescriptor == null )
        {
            _firstActiveDescriptor = d;
        }
        else
        {
            Throw.DebugAssert( _firstActiveDescriptor._prevActiveDescriptor == null );
            d._nextActiveDescriptor = _firstActiveDescriptor;
            _firstActiveDescriptor._prevActiveDescriptor = d;
            _firstActiveDescriptor = d;
        }
    }

    bool AddObject( IActivityMonitor monitor, ICachedType oT, object o, LoopContext? loop )
    {
        if( !_hasError )
        {
            var d = _firstActiveDescriptor;
            while( d != null )
            {
                d.OnObjectAppear( monitor, this, oT, o );
                d = d._nextActiveDescriptor;
            }
        }
        return !_hasError;
    }

    public bool CanRun => !_hasError && _oldReadyToRun.Count > 0;

    public bool RunOne( IActivityMonitor monitor )
    {
        Throw.CheckState( CanRun );
        var c = _oldReadyToRun.Dequeue();
        return c.Run( monitor, this );
    }

    void AddReadyToRun( OldCallable callable )
    {
        _oldReadyToRun.Enqueue( callable );
    }

    void AddReadyToRun( Callable callable )
    {
        _readyToRun.Enqueue( callable );
    }
}
