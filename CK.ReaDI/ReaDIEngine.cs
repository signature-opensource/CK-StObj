using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    readonly GlobalTypeCache _typeCache;
    readonly TypeRegistrar _typeRegistrar;
    // Using a FIFO offers determinism.
    readonly Queue<Callable> _readyToRun;
    bool _hasError;

    public ReaDIEngine( GlobalTypeCache typeCache )
    {
        _typeCache = typeCache;
        _typeRegistrar = new TypeRegistrar( typeCache );
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

    public bool AddObject( IActivityMonitor monitor, object o )
    {
        Throw.CheckState( HasError is false );
        var oT = _typeCache.Get( o.GetType() );

        ParameterType? parameterType = null;
        if( o is IReaDIHandler handler )
        {
            if( !_typeRegistrar.RegisterHandlerType( monitor, oT, handler, out var handlerType ) )
            {
                return SetError( monitor );
            }
            if( handlerType.CurrentHandler == handler )
            {
                // We just created the handlerType or we are on a duplicate
                // AddObject instance (not a "Duplicate Activation error"):
                // nothing change, it is useless to continue.
                return true;
            }
            // This handler is also a LoopParameterType or this is a "Duplicate Activation error"
            // that will be handled below.
            // If the handler is a loop parameter, avoid the parameter type lookup.
            parameterType = handlerType.LoopParameter?.Parameter;
        }
        if( parameterType == null )
        {
            parameterType = _typeRegistrar.FindParameter( oT );
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
