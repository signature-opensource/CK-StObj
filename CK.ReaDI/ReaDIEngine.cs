using CK.Engine.TypeCollector;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using static CK.Core.CompletionSource;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    readonly GlobalTypeCache _typeCache;
    readonly TypeRegistrar _typeRegistrar;

    Dictionary<ICachedType, object> _waitingObjects;
    // Using a FIFO offers determinism.
    readonly Queue<Callable> _readyToRun;
    int _waitingCallableCount;
    readonly bool _debugMode;
    bool _hasError;

    public ReaDIEngine( GlobalTypeCache typeCache, bool debugMode = false )
    {
        _typeCache = typeCache;
        _debugMode = debugMode;
        _typeRegistrar = new TypeRegistrar( typeCache );
        _waitingObjects = new Dictionary<ICachedType, object>();
        _readyToRun = new Queue<Callable>( 128 );
    }

    public bool HasError => _hasError;

    public bool IsCompleted => _hasError || (_waitingCallableCount == 0 && _readyToRun.Count == 0 && _waitingObjects.Count == 0);

    public bool IsSuccessfullyCompleted => !_hasError && _waitingCallableCount == 0 && _readyToRun.Count == 0 && _waitingObjects.Count == 0;

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
        Throw.CheckNotNullArgument( o );
        var oT = _typeCache.Get( o.GetType() );
        if( !_typeRegistrar.CheckIntrinsicType( monitor, oT ) )
        {
            return false;
        }
        bool isHandler = false;
        ParameterType? parameterType = null;
        if( o is IReaDIHandler handler )
        {
            if( !_typeRegistrar.RegisterHandlerType( monitor, this, oT, handler, out var handlerType ) )
            {
                return SetError( monitor );
            }
            isHandler = true;
            // If the handler is a loop parameter, avoid the parameter type lookup.
            parameterType = handlerType.LoopParameter?.Parameter;
        }
        else if( _debugMode )
        {
            var reaDIMethods = oT.DeclaredMembers.Where( m => m.AttributesData.Any( a => a.AttributeType == typeof( ReaDIAttribute ) ) );
            if( reaDIMethods.Any() )
            {
                monitor.Error( $"""
                    Type '{oT}' has [ReaDI] methods, it must implement the '{nameof(IReaDIHandler)}' interface:
                    {reaDIMethods.Select( m => m.ToString() ).Concatenate( Environment.NewLine)}
                    """ );
                return false;
            }
        }
        if( parameterType == null )
        {
            if( !_typeRegistrar.ParameterTypes.TryGetValue( oT, out parameterType ) )
            {
                foreach( var p in _typeRegistrar.ParameterTypes.Values )
                {
                    if( TypeMatch( oT, p.Type ) )
                    {
                        parameterType = p;
                    }
                }
            }
        }

        // If this type of object has currently no parameter type,
        // we register it in the waiting list.
        if( parameterType == null )
        {
            // Don't store the handlers in the waiting object list: we have them
            // and if they are not consumed, this is "normal", they act as "optional objects".
            if( !isHandler ) _waitingObjects.Add( oT, o );
        }
        else
        {
            if( _debugMode
                && !CheckSingleParameterTypeMatch( monitor, oT, parameterType.Type,  parameterType.Definer ) )
            {
                return false;
            }
            if( !parameterType.SetCurrentValue( monitor, this, o ) )
            {
                return SetError( monitor );
            }
        }
        return true;
    }

    public ReaDIEngineState GetState() => CanRun ? ReaDIEngineState._canRunState : new ReaDIEngineState( this );

    internal IReadOnlyDictionary<ICachedType, object> WaitingObjects => _waitingObjects;

    internal IEnumerable<IReaDIMethod> AllCallables => _typeRegistrar.AllCallables;

    void AddReadyToRun( Callable callable )
    {
        _readyToRun.Enqueue( callable );
    }

    bool FindWaitingObjectFor( IActivityMonitor monitor, ICachedType parameterType, CachedParameterInfo definer, out object? initialValue )
    {
        ICachedType? foundInputType = parameterType;
        // A waiting object with the exact type may he available.
        // If not, it can be a handler (if the handler has a current value).
        if( !_waitingObjects.TryGetValue( parameterType, out initialValue )
            && (!_typeRegistrar.Handlers.TryGetValue( parameterType, out var h )
                || (initialValue = h.CurrentHandler) == null) )
        {
            // If not, we challenge all the waiting objects and the handlers.
            foreach( var (oT,o) in _waitingObjects.Concat( _typeRegistrar.HandlersAsWaitingObjects ) )
            {
                if( TypeMatch( oT, parameterType ) )
                {
                    initialValue = o;
                    foundInputType = oT;
                    break;
                }
            }
        }
        return !_debugMode
               || initialValue == null
               || CheckSingleParameterTypeMatch( monitor, foundInputType, parameterType, definer );
    }

    bool TypeMatch( ICachedType inputType, ICachedType parameterType )
    {
        return inputType == parameterType
               || inputType.ConcreteGeneralizations.Contains( parameterType );
    }

    bool CheckSingleParameterTypeMatch( IActivityMonitor monitor,
                                        ICachedType inputType,
                                        ICachedType alreadyMatchedType,
                                        CachedParameterInfo alreadyMatchedDefiner )
    {
        List<ParameterType>? onError = null;
        foreach( var p in _typeRegistrar.ParameterTypes.Values )
        {
            if( alreadyMatchedType != p.Type && TypeMatch( inputType, p.Type ) )
            {
                onError ??= new List<ParameterType>();
                onError.Add( p );
            }
        }
        if( onError != null )
        {
            var conflicts = onError.Select( p => $"'{p.Type}' of {p}" );
            monitor.Error( $"""
                    Ambiguous added object. Its type '{inputType}' can satisify more than one [ReaDI] parameters:
                    {alreadyMatchedDefiner} of '{alreadyMatchedDefiner.Name}' in '{alreadyMatchedDefiner.Method}'
                    {conflicts.Concatenate( Environment.NewLine )}
                    """ );
            return false;
        }
        return true;
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
