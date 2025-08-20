using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core;

public sealed class ReaDIEngineState
{
    readonly UncompletedReason _reason;
    readonly IReadOnlyList<object> _waitingObjects;
    readonly IReadOnlyList<IReaDIMethod> _waitingMethods;
    readonly IReaDIMethod? _errorMethod;

    internal static ReaDIEngineState _canRunState = new ReaDIEngineState();

    ReaDIEngineState()
    {
        _waitingObjects = [];
        _waitingMethods = [];
    }

    internal ReaDIEngineState( ReaDIEngine e )
    {
        Throw.DebugAssert( !e.CanRun );
        if( e.HasError )
        {
            _reason = UncompletedReason.Error;
        }
        List<IReaDIMethod>? waitingMethods = null;
        foreach( var m in e.AllCallables )
        {
            if( m.IsError )
            {
                Throw.DebugAssert( _errorMethod == null );
                _errorMethod = m;
            }
            else if( m.IsWaiting )
            {
                waitingMethods ??= new List<IReaDIMethod>();
                waitingMethods.Add( m );
            }
        }
        _waitingMethods = waitingMethods ?? [];
        if( !e.HasError && _waitingMethods.Count > 0 ) _reason |= UncompletedReason.HasWaitingMethods;

        _waitingObjects = e.WaitingObjects.Values.ToArray();
        if( !e.HasError && _waitingObjects.Count > 0 ) _reason |= UncompletedReason.HasWaitingObjects;
    }

    [Flags]
    public enum UncompletedReason
    {
        /// <summary>
        /// <see cref="ReaDIEngine.IsSuccessfullyCompleted"/> (it is complete)
        /// or <see cref="ReaDIEngine.CanRun"/> (the engine's run methods must be called)
        /// are true.
        /// </summary>
        None,

        /// <summary>
        /// A severe error stopped the execution.
        /// </summary>
        Error = 1,

        /// <summary>
        /// One or more [ReaDI] methods have not been called.
        /// </summary>
        HasWaitingMethods = 32,

        /// <summary>
        /// Some added objects have not been consumed.
        /// </summary>
        HasWaitingObjects = 64
    }

    public UncompletedReason UnsuccessfulCompletedReason => _reason;

    public IReadOnlyList<object> WaitingObjects => _waitingObjects;

    public IReadOnlyList<IReaDIMethod> WaitingMethods => _waitingMethods;

    public IReaDIMethod? ErrorMethod => _errorMethod;
}
