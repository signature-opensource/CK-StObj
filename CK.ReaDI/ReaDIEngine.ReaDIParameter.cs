using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    abstract class ParameterType
    {
        readonly ICachedType _type;

        // This can be updated when a ReaDILoop<,> appears
        // when a stateless [ReaDI] parameter was registered.
        CachedParameterInfo _definer;
        LoopParameterType? _loopParameter;
        object? _currentValue;

        // Should we optimize this naïve implementation?
        readonly record struct Slot( Callable C, int Index );
        readonly List<Slot> _slots;

        internal ParameterType? _nextActiveDescriptor;
        internal ParameterType? _prevActiveDescriptor;

        ParameterType( ICachedType type, ICachedType? loopStateType, CachedParameterInfo definer )
        {
            _type = type;
            _definer = definer;
            if( loopStateType != null )
            {
                _loopParameter = new LoopParameterType( this, loopStateType );
            }
            _slots = [];
        }

        public ICachedType? Type => _type;

        [MemberNotNullWhen( true, nameof( LoopParameter ) )]
        public bool IsLoopParameter => _loopParameter != null;

        public LoopParameterType? LoopParameter => _loopParameter;

        public bool Match( ICachedType oT ) => _type == oT || ContravariantMatch( oT );

        public bool OnObjectAppear( IActivityMonitor monitor, ReaDIEngine engine, ICachedType oT, object o )
        {
            Throw.DebugAssert( Match( oT ) );
            if( _currentValue != null )
            {
                monitor.Error( _type == oT
                                ? $"Duplicate '{oT}' object added. This object is not a loop, at most one can exist at the same time."
                                : $"Duplicate '{oT}' object added. This object is not a loop, at most one '{_type}' can exist at the same time.");
                return false;
            }
            _currentValue = o;
            foreach( var (callable, index) in _slots )
            {
                callable.SetArgument( engine, index, o );
            }
            return true;
        }

        protected abstract bool ContravariantMatch( ICachedType o );

        sealed class Exact : ParameterType
        {
            public Exact( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
                : base( type, loopStateType, loopDefiner )
            {
                Throw.DebugAssert( type.Type.IsValueType || type.Type.IsSealed );
            }

            protected override bool ContravariantMatch( ICachedType o ) => Throw.NotSupportedException<bool>();
        }

        sealed class VariantClass : ParameterType
        {
            public VariantClass( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
                : base( type, loopStateType, loopDefiner )
            {
                Throw.DebugAssert( !type.Type.IsValueType && !type.Type.IsSealed && !type.Type.IsInterface );
            }

            protected override bool ContravariantMatch( ICachedType o )
            {
                var b = o.BaseType;
                while( b != null )
                {
                    if( o == b ) return true;
                    b = b.BaseType;
                }
                return false;
            }
        }

        sealed class VariantInterface : ParameterType
        {
            public VariantInterface( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
                : base( type, loopStateType, loopDefiner )
            {
                Throw.DebugAssert( type.Type.IsInterface );
            }

            protected override bool ContravariantMatch( ICachedType o ) => o.Interfaces.Contains( _type );
        }

        public bool CheckLoopStateType( IActivityMonitor monitor,
                                        GlobalTypeCache typeCache,
                                        ICachedType? loopStateType,
                                        CachedParameterInfo p )
        {
            var thisLoopStateType = _loopParameter?.LoopStateType;
            // Same (including null-null). No question ask.
            if( loopStateType == thisLoopStateType )
            {
                return true;
            }
            if( thisLoopStateType == null || loopStateType == null )
            {
                var pReg = _definer;
                // Switch the regular and the loop.
                if( loopStateType == null ) (p, pReg) = (pReg, p);
                monitor.Error( $"""
                    Method '{pReg.Method.ToStringWithDeclaringType()}' defines its '{pReg.Name}' parameter as a regular parameter but method
                    '{p.Method.ToStringWithDeclaringType()}' defines its '{p.Name}' as a loop parameter. All loop parameters must be coherent.
                    """ );
                return false;
            }
            // The current one was a stateless [ReaDI]. Types the state.
            if( thisLoopStateType == typeCache.KnownTypes.Void )
            {
                Throw.DebugAssert( _loopParameter != null );
                _definer = p;
                _loopParameter.SetLoopStateType( loopStateType );
                return true;
            }
            // The new one is stateless. It's fine.
            if( loopStateType == typeCache.KnownTypes.Void )
            {
                return true;
            }
            // The two states differ. We don't play any variance game here:
            // the state types must be exactly the same.
            if( thisLoopStateType != loopStateType )
            {
                monitor.Error( $"""
                    Method '{_definer.Method}' defines its '{_definer.Name}' parameter as loop parameter with a '{thisLoopStateType}' state but method
                    '{p.Method.ToStringWithDeclaringType()}' defines its '{p.Name}' as a loop parameter with a '{loopStateType}' state.
                    Loop state must be exactly the same (or, if th state is not used, a stateless [ReaDILoop] attribute can be used by any of them).
                    """ );
                return false;
            }
            return true;
        }

        public void AddCallableParameter( Callable c, int idx )
        {
            _slots.Add( new Slot( c, idx ) );
        }

        /// <summary>
        /// Handles a [ReaDILoop] attribute and/or a <see cref="ReaDILoop{T, TState}"/> parameter.
        /// </summary>
        internal static (ICachedType ParameterType, ICachedType? loopStateType) GetLoopTypes( GlobalTypeCache typeCache, CachedParameterInfo p )
        {
            ICachedType? loopStateType = null;
            if( p.AttributesData.Any( a => a.AttributeType == typeof( ReaDILoopAttribute ) ) )
            {
                loopStateType = typeCache.KnownTypes.Void;
            }
            var parameterType = p.ParameterType;
            if( parameterType.GenericTypeDefinition?.Type == typeof( ReaDILoop<,> ) )
            {
                loopStateType = parameterType.GenericArguments[1];
                parameterType = parameterType.GenericArguments[0];
            }
            return (parameterType, loopStateType);
        }

        internal static ParameterType? Create( IActivityMonitor monitor,
                                                GlobalTypeCache typeCache,
                                                ICachedType actualParameterType,
                                                ICachedType? loopStateType,
                                                CachedParameterInfo p )
        {
            var t = actualParameterType.Type;
            if( actualParameterType.EngineUnhandledType != EngineUnhandledType.None
                || actualParameterType == typeCache.KnownTypes.Object
                || t.IsValueType
                || !(t.IsInterface || t.IsClass)
                || t.IsByRef
                || t.IsByRefLike
                || t.IsArray
                || t.IsVariableBoundArray
                || actualParameterType.GenericTypeDefinition == typeCache.KnownTypes.GenericIEnumerableDefinition )
            {
                if( p.ParameterType == actualParameterType )
                {
                    monitor.Error( $"""
                        Invalid [ReaDI] method parameter '{actualParameterType.Name} {p.Name}' in '{p.Method.ToStringWithDeclaringType()}'.
                        Parameter can only be interfaces or regular classes (and not object).
                        """ );
                }
                else
                {
                    monitor.Error( $"""
                        Invalid [ReaDI] method parameter '{p.ParameterType.Name} {p.Name}' in '{p.Method.ToStringWithDeclaringType()}'.
                        Type '{actualParameterType}' must be an interface or a regular classes (and not object).
                        """ );
                }
                return null;
            }
            return t.IsSealed
                    ? new Exact( actualParameterType, loopStateType, p )
                    : t.IsInterface
                        ? new VariantInterface( actualParameterType, loopStateType, p )
                        : new VariantClass( actualParameterType, loopStateType, p );
        }

    }

    sealed class Callable
    {
        readonly HandlerType _handler;
        readonly ICachedMethodInfo _method;
        readonly ImmutableArray<ParameterType> _parameters;
        readonly object?[] _args;
        internal Callable? _next;
        int _missingCount;

        internal Callable( HandlerType handler,
                           ICachedMethodInfo method,
                           ParameterType[] parameters )
        {
            _handler = handler;
            _method = method;
            _parameters = ImmutableCollectionsMarshal.AsImmutableArray( parameters );
            _args = new object[_missingCount = method.ParameterInfos.Length];
        }

        public HandlerType Handler => _handler;

        public bool IsWaiting => _missingCount != 0;

        public ICachedMethodInfo Method => _method;

        public Callable? NextCallable => _next;

        public ImmutableArray<ParameterType> Parameters => _parameters;

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


    sealed class HandlerType
    {
        readonly ICachedType _type;
        Callable? _firstLoopCallable;
        Callable? _firstRegularCallable;
        IReaDIHandler? _currentHandler;

        HandlerType( ICachedType type )
        {
            _type = type;
        }

        public IReaDIHandler? CurrentHandler => _currentHandler;

        public ICachedType Type => _type;

        public Callable? FirstLoopCallable => _firstLoopCallable;

        public Callable? FirstRegularCallable => _firstRegularCallable;

        void AddCallable( Callable c, bool isLoopCallable )
        {
            if( isLoopCallable )
            {
                c._next = _firstLoopCallable;
                _firstLoopCallable = c;
            }
            else
            {
                c._next = _firstRegularCallable;
                _firstRegularCallable = c;
            }
        }

        internal static HandlerType? Create( IActivityMonitor monitor,
                                             GlobalTypeCache typeCache,
                                             Dictionary<ICachedType, ParameterType> parameters,
                                             ICachedType type )
        {
            bool success = true;
            var handlerType = new HandlerType( type );
            foreach( var m in type.DeclaredMembers.OfType<ICachedMethodInfo>() )
            {
                if( m.AttributesData.Any( a => a.AttributeType == typeof( ReaDIAttribute ) ) )
                {
                    if( m.MethodInfo.IsGenericMethodDefinition )
                    {
                        monitor.Error( $"[ReaDI] cannot be set on method '{m}' because it is a generic method definition." );
                        success = false;
                    }
                    else if( m.IsAsynchronous )
                    {
                        monitor.Error( $"[ReaDI] cannot be set on method '{m}' because it is an asynchronous method." );
                        success = false;
                    }
                    else
                    {
                        var parameterTypes = new ParameterType[m.ParameterInfos.Length];
                        var callable = new Callable( handlerType, m, parameterTypes );
                        success &= FindOrCreateParameters( monitor, typeCache, parameters, callable, parameterTypes, out var isLoopCallable );
                        if( success )
                        {
                            handlerType.AddCallable( callable, isLoopCallable );
                        }
                    }
                }
            }
            return success ? handlerType : null;

            static bool FindOrCreateParameters( IActivityMonitor monitor,
                                                GlobalTypeCache typeCache,
                                                Dictionary<ICachedType, ParameterType> parameters,
                                                Callable callable,
                                                ParameterType[] parameterTypes,
                                                out bool isLoopCallable )
            {
                isLoopCallable = false;
                bool success = true;
                var parameterInfos = callable.Method.ParameterInfos;
                for( int i = 0; i < parameterInfos.Length; i++ )
                {
                    CachedParameterInfo paramInfo = parameterInfos[i];
                    var (parameterType, loopStateType) = ParameterType.GetLoopTypes( typeCache, paramInfo );
                    if( parameters.TryGetValue( parameterType, out var p ) )
                    {
                        if( !p.CheckLoopStateType( monitor, typeCache, loopStateType, paramInfo ) )
                        {
                            success = false;
                            continue;
                        }
                    }
                    else
                    {
                        p = ParameterType.Create( monitor, typeCache, parameterType, loopStateType, paramInfo );
                        if( p == null )
                        {
                            success = false;
                            continue;
                        }
                    }
                    isLoopCallable = p.IsLoopParameter;
                    parameters.Add( parameterType, p );
                    parameterTypes[i] = p;
                    p.AddCallableParameter( callable, i );
                }
                return success;
            }
        }
    }

    /// <summary>
    /// This structure is additive but internally mutable.
    /// </summary>
    sealed class ReaDITypeRegistrar
    {
        readonly Dictionary<ICachedType, HandlerType> _handlers;
        readonly Dictionary<ICachedType, ParameterType> _parameters;
        readonly LoopTree _loopTree;

        public ReaDITypeRegistrar()
        {
            _handlers = new Dictionary<ICachedType, HandlerType>();
            _parameters = new Dictionary<ICachedType, ParameterType>();
            _loopTree = new LoopTree();
        }

        public bool RegisterHandlerType( IActivityMonitor monitor,
                                         GlobalTypeCache typeCache,
                                         ICachedType type,
                                         [NotNullWhen(true)]out HandlerType? handler )
        {
            if( !_handlers.TryGetValue( type, out handler ) )
            {
                handler = HandlerType.Create( monitor, typeCache, _parameters, type );
                if( handler == null )
                {
                    return false;
                }
                if( handler.FirstLoopCallable != null
                    && !HandleLoopCallable( monitor, handler ) )
                {
                    return false;
                }
                _handlers.Add( type, handler );
            }
            return true;

            bool HandleLoopCallable( IActivityMonitor monitor, HandlerType handler )
            {
                bool success = true;
                var loopParameters = new List<LoopParameterType>();
                var c = handler.FirstLoopCallable;
                Throw.DebugAssert( c != null );
                do
                {
                    foreach( var p in c.Parameters )
                    {
                        if( p.IsLoopParameter ) loopParameters.Add( p.LoopParameter );
                    }
                    if( loopParameters.Count > 0 )
                    {
                        success &= _loopTree.HandleNewLoopParameters( monitor, loopParameters );
                        loopParameters.Clear();
                    }
                    c = c.NextCallable;
                }
                while( c != null );
                return success;
            }
        }
    }

    sealed class LoopTree
    {
        readonly List<LoopParameterType> _roots;

        public LoopTree()
        {
            _roots = new List<LoopParameterType>();
        }

        internal bool HandleNewLoopParameters( IActivityMonitor monitor, List<LoopParameterType> loopParameters )
        {
            
        }

    }

    sealed class LoopParameterType
    {
        readonly ParameterType _parameter;
        ICachedType _loopStateType;

        LoopTree? _tree;
        LoopParameterType? _parent;
        List<LoopParameterType>? _children;

        public LoopParameterType( ParameterType parameter, ICachedType loopStateType )
        {
            Throw.DebugAssert( parameter.IsLoopParameter );
            _parameter = parameter;
            _loopStateType = loopStateType;
        }

        public LoopParameterType? Parent => _parent;

        public IReadOnlyList<LoopParameterType> Children => _children ?? [];

        public ParameterType Parameter => _parameter;

        public ICachedType LoopStateType => _loopStateType;

        internal void SetLoopStateType( ICachedType loopStateType )
        {
            Throw.DebugAssert( "Can only transition from a void to a typed state.", _loopStateType.Type == typeof(void) );
            _loopStateType = loopStateType;
        }
    }
}

