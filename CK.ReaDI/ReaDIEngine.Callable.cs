using CK.Engine.TypeCollector;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class Callable : IReaDIMethod
    {
        readonly HandlerType _handler;
        readonly CachedMethod _method;
        readonly ImmutableArray<ParameterType> _parameters;
        readonly object?[] _args;
        internal Callable? _next;
        int _monitorIdx;
        int _missingCount;
        Flags _flags;

        [Flags]
        enum Flags
        {
            IsLoopCallable = 1,
            IsWaiting = 2,
            IsRunning = 4,
            IsCompleted = 8,
            IsError = 16
        }

        // Regular constructor.
        internal Callable( HandlerType handler,
                           CachedMethod method,
                           ParameterType[] parameters )
        {
            _handler = handler;
            _method = method;
            _parameters = ImmutableCollectionsMarshal.AsImmutableArray( parameters );
            _args = new object[_missingCount = method.ParameterInfos.Length];
            _next = handler.FirstCallable;
            handler._firstCallable = this;
        }

        ICachedType IReaDIMethod.Handler => _handler.Type;

        public CachedMethod Method => _method;

        public bool IsLoopCallable => (_flags & Flags.IsLoopCallable) != 0;

        public bool IsWaiting => (_flags & Flags.IsWaiting) != 0;

        public bool IsRunning => (_flags & Flags.IsRunning) != 0;

        public bool IsCompleted => (_flags & Flags.IsCompleted) != 0;

        public bool IsError => (_flags & Flags.IsError) != 0;

        public Callable? NextCallable => _next;

        public ImmutableArray<ParameterType> Parameters => _parameters;

        internal void Initialize( ReaDIEngine engine, int idxMonitor, int idxEngine, bool isLoopCallable )
        {
            engine._waitingCallableCount++;
            if( isLoopCallable )
            {
                _flags |= Flags.IsLoopCallable;
            }
            _monitorIdx = idxMonitor;
            if( idxMonitor >= 0 )
            {
                --_missingCount;
            }
            if( idxEngine >= 0 )
            {
                _args[idxEngine] = engine;
                --_missingCount;
            }
            for( int i = 0; i < _parameters.Length; i++ )
            {
                if( i != idxMonitor && i != idxEngine )
                {
                    ParameterType param = _parameters[i];
                    if( param.CurrentValue != null )
                    {
                        _args[i] = param.CurrentValue;
                        --_missingCount;
                    }
                }
            }
            Throw.DebugAssert( _missingCount >= 0 );
            _flags |= Flags.IsWaiting;
            if( _missingCount == 0 )
            {
                WaitToRun( engine );
            }
        }

        internal void SetArgument( ReaDIEngine engine, int idxAttr, object o )
        {
            Throw.DebugAssert( o != null && _missingCount > 0 );
            ref var instance = ref _args[idxAttr];
            if( instance == null ) --_missingCount;
            instance = o;
            if( _missingCount == 0 )
            {
                WaitToRun( engine );
            }
        }

        void WaitToRun( ReaDIEngine engine )
        {
            Throw.DebugAssert( (_flags & Flags.IsWaiting) != 0 );
            _flags &= ~Flags.IsWaiting;
            _flags |= Flags.IsRunning;
            engine._waitingCallableCount--;
            engine.AddReadyToRun( this );
        }

        internal bool Run( IActivityMonitor monitor, ReaDIEngine engine )
        {
            Throw.DebugAssert( (_flags & Flags.IsRunning) != 0 );
            try
            {
                if( _monitorIdx >= 0 ) _args[_monitorIdx] = monitor; 
                _method.MethodInfo.Invoke( _handler.CurrentHandler, BindingFlags.DoNotWrapExceptions, null, _args, null );
                _flags |= Flags.IsCompleted;
                return true;
            }
            catch( Exception ex )
            {
                monitor.Error( $"While calling '{_method.ToStringWithDeclaringType()}'.", ex );
                _flags |= Flags.IsError;
                return engine.SetError( monitor );
            }
        }

        public override string ToString() => _method.ToStringWithDeclaringType();
    }
}

