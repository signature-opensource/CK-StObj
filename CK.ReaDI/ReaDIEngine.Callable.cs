using CK.Engine.TypeCollector;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class Callable
    {
        readonly HandlerType _handler;
        readonly ICachedMethodInfo _method;
        readonly ImmutableArray<ParameterType> _parameters;
        readonly object?[] _args;
        internal Callable? _next;
        int _monitorIdx;
        int _missingCount;

        internal Callable( HandlerType handler,
                           ICachedMethodInfo method,
                           ParameterType[] parameters )
        {
            _handler = handler;
            _method = method;
            _parameters = ImmutableCollectionsMarshal.AsImmutableArray( parameters );
            _args = new object[_missingCount = method.ParameterInfos.Length];
            _next = handler.FirstCallable;
            handler._firstCallable = this;
        }

        public HandlerType Handler => _handler;

        public bool IsWaiting => _missingCount != 0;

        public ICachedMethodInfo Method => _method;

        public Callable? NextCallable => _next;

        public ImmutableArray<ParameterType> Parameters => _parameters;

        internal void Initialize( ReaDIEngine engine, int idxMonitor, int idxEngine )
        {
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
                ParameterType param = _parameters[i];
                if( param.CurrentValue != null )
                {
                    _args[i] = param.CurrentValue;
                    --_missingCount;
                }
            }
            Throw.DebugAssert( _missingCount >= 0 );
            if( _missingCount == 0 )
            {
                engine.AddReadyToRun( this );
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
                engine.AddReadyToRun( this );
            }
        }

        internal bool Run( IActivityMonitor monitor, ReaDIEngine engine )
        {
            try
            {
                if( _monitorIdx >= 0 ) _args[_monitorIdx] = monitor; 
                _method.MethodInfo.Invoke( _handler.CurrentHandler, BindingFlags.DoNotWrapExceptions, null, _args, null );
                return true;
            }
            catch( Exception ex )
            {
                monitor.Error( $"While calling '{_method.ToStringWithDeclaringType()}'.", ex );
                return engine.SetError( monitor );
            }
        }

        public override string ToString() => _method.ToStringWithDeclaringType();
    }
}

