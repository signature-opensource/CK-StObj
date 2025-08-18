using CK.Engine.TypeCollector;
using System;
using System.Reflection;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class OldCallable
    {
        readonly CallableHost _host;
        readonly ICachedMethodInfo _method;
        readonly object?[] _args;
        OldCallable? _nextInHost;
        int _missingCount;

        internal OldCallable( CallableHost host, ICachedMethodInfo method )
        {
            _host = host;
            _nextInHost = host.Head;
            _method = method;
            _args = new object[_missingCount = method.ParameterInfos.Length];
        }

        internal CallableHost Host => _host;

        public bool IsWaiting => _missingCount != 0;

        public ICachedMethodInfo Method => _method;

        internal void SetArgument( ReaDIEngine engine, int idxAttr, object o )
        {
            Throw.DebugAssert( o != null );
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
                _method.MethodInfo.Invoke( _host.Handler, BindingFlags.DoNotWrapExceptions, null, _args, null );
                return true;
            }
            catch( Exception ex )
            {
                monitor.Error( $"While calling '{_method.DeclaringType}.{_method}'.", ex );
                return engine.SetError( monitor );
            }
        }

        public override string ToString() => $"{_method} of {_method.DeclaringType}";
    }
}
