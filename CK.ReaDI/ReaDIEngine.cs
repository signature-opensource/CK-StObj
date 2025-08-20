using CK.Engine.TypeCollector;
using System.Collections.Generic;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    readonly GlobalTypeCache _typeCache;
    readonly TypeRegistrar _typeRegistrar;

    Dictionary<ICachedType, object> _waitingObjects;
    // Using a FIFO offers determinism.
    readonly Queue<Callable> _readyToRun;
    bool _hasError;

    public ReaDIEngine( GlobalTypeCache typeCache )
    {
        _typeCache = typeCache;
        _typeRegistrar = new TypeRegistrar( typeCache );
        _waitingObjects = new Dictionary<ICachedType, object>();
        _readyToRun = new Queue<Callable>( 128 );
    }

    public bool HasError => _hasError;

    public bool CanRun => !_hasError && _readyToRun.Count > 0;

    public bool RunOne( IActivityMonitor monitor )
    {
        Throw.CheckState( CanRun );
        var c = _readyToRun.Dequeue();
        return c.Run( monitor, this );
    }

    public bool RunAll( IActivityMonitor monitor )
    {
        Throw.CheckState( CanRun );
        do
        {
            var c = _readyToRun.Dequeue();
            if( !c.Run( monitor, this ) )
            {
                return false;
            }
        }
        while( CanRun );
        return true;
    }

    public bool AddObject( IActivityMonitor monitor, object o )
    {
        Throw.CheckState( HasError is false );
        var oT = _typeCache.Get( o.GetType() );

        ParameterType? parameterType = null;
        if( o is IReaDIHandler handler )
        {
            if( !_typeRegistrar.RegisterHandlerType( monitor, this, oT, handler, out var handlerType ) )
            {
                return SetError( monitor );
            }
            // If the handler is a loop parameter, avoid the parameter type lookup.
            parameterType = handlerType.LoopParameter?.Parameter;
        }
        // No contravariant handling for the moment.
        parameterType ??= _typeRegistrar.FindParameter( oT );

        // If this type of object has currently no parameter type,
        // we register it in the waiting list.
        if( parameterType == null )
        {
            _waitingObjects.Add( oT, o );
        }
        else
        {
            if( !parameterType.SetCurrentValue( monitor, this, o ) )
            {
                return SetError( monitor );
            }
        }
        return true;
    }

    void AddReadyToRun( Callable callable )
    {
        _readyToRun.Enqueue( callable );
    }

    bool SetError( IActivityMonitor monitor )
    {
        if( !_hasError )
        {
            _hasError = true;
            monitor.Error( $"ReaDIEngine is on error." );
        }
        return false;
    }


}
