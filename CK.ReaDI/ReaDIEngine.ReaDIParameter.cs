using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    abstract class ReaDIParameter
    {
        readonly ICachedType _type;
        object? _value;

        CachedParameterInfo _loopDefiner;
        ICachedType? _loopStateType;
        object? _loopState;

        // Should we optimize this naïve implementation?
        readonly record struct Slot( Callable C, int Index );
        readonly List<Slot> _slots;

        internal ReaDIParameter? _nextActiveDescriptor;
        internal ReaDIParameter? _prevActiveDescriptor;

        ReaDIParameter( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
        {
            _type = type;
            _loopStateType = loopStateType;
            _loopDefiner = loopDefiner;
            _slots = [];
        }

        internal object? Value => _value;

        public ICachedType? LoopStateType => _loopStateType;

        internal bool OnObjectAppear( IActivityMonitor monitor, ReaDIEngine engine, ICachedType oT, object o, out bool matched )
        {
            bool exactType = _type == oT;
            if( exactType || ContravariantMatch( oT ) )
            {
                matched = true;
                if( _loopStateType != null )
                {
                }
                else
                {
                    if( _value != null )
                    {
                        monitor.Error( exactType
                                        ? $"Duplicate '{oT}' object added. This object is not a loop, at most one can exist at the same time."
                                        : $"Duplicate '{oT}' object added. This object is not a loop, at most one '{_type}' can exist at the same time.");
                        return false;
                    }
                    _value = o;
                    foreach( var (callable, index) in _slots )
                    {
                        callable.SetArgument( engine, index, o );
                    }
                }
                return true;
            }
            matched = false;
            return true;
        }

        internal protected abstract bool ContravariantMatch( ICachedType o );

        public void AddCallableParameter( ReaDIEngine engine, Callable c, int idx )
        {
            _slots.Add( new Slot( c, idx ) );
            if( _slots.Count == 1 )
            {
                engine.Activate( this );
            }
        }

        internal void OnRemoveHost( ReaDIEngine engine, CallableHost host )
        {
            Throw.DebugAssert( host.Loop != null );
            for( int i = 0; i < _slots.Count; i++ )
            {
                if( _slots[i].C.Host == host )
                {
                    _slots.RemoveAt( i-- );
                }
            }
            if( _slots.Count == 0 )
            {
                engine.Deactivate( this );
            }
        }

        sealed class Exact : ReaDIParameter
        {
            public Exact( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
                : base( type, loopStateType, loopDefiner )
            {
                Throw.DebugAssert( type.Type.IsValueType || type.Type.IsSealed );
            }

            protected internal override bool ContravariantMatch( ICachedType o ) => Throw.NotSupportedException<bool>();
        }

        sealed class VariantClass : ReaDIParameter
        {
            public VariantClass( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
                : base( type, loopStateType, loopDefiner )
            {
                Throw.DebugAssert( !type.Type.IsValueType && !type.Type.IsSealed && !type.Type.IsInterface );
            }

            protected internal override bool ContravariantMatch( ICachedType o )
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

        sealed class VariantInterface : ReaDIParameter
        {
            public VariantInterface( ICachedType type, ICachedType? loopStateType, CachedParameterInfo loopDefiner )
                : base( type, loopStateType, loopDefiner )
            {
                Throw.DebugAssert( type.Type.IsInterface );
            }

            protected internal override bool ContravariantMatch( ICachedType o ) => o.Interfaces.Contains( _type );
        }

        internal bool CheckLoopStateType( IActivityMonitor monitor,
                                          GlobalTypeCache typeCache,
                                          ICachedType? loopStateType,
                                          CachedParameterInfo p )
        {
            // Same (including null-null). No question ask.
            if( loopStateType == _loopStateType )
            {
                return true;
            }
            if( _loopStateType == null || loopStateType == null )
            {
                var pReg = _loopDefiner;
                // Switch the regular and the loop.
                if( loopStateType == null ) (p, pReg) = (pReg, p);
                monitor.Error( $"""
                    Method '{pReg.Method.ToStringWithDeclaringType()}' defines its '{pReg.Name}' parameter as a regular parameter but method
                    '{p.Method.ToStringWithDeclaringType()}' defines its '{p.Name}' as a loop parameter. All loop parameters must be coherent.
                    """ );
                return false;
            }
            // The current one was a stateless [ReaDI]. Types the state.
            if( _loopStateType == typeCache.KnownTypes.Void )
            {
                _loopDefiner = p;
                _loopState = loopStateType;
                return true;
            }
            // The new one is stateless. It's fine.
            if( loopStateType == typeCache.KnownTypes.Void )
            {
                return true;
            }
            // The two states differ. We don't play any variance game here:
            // the state types must be exactly the same.
            if( _loopStateType != loopStateType )
            {
                monitor.Error( $"""
                    Method '{_loopDefiner.Method}' defines its '{_loopDefiner.Name}' parameter as loop parameter with a '{_loopStateType}' state but method
                    '{p.Method.ToStringWithDeclaringType()}' defines its '{p.Name}' as a loop parameter with a '{loopStateType}' state.
                    Loop state must be exactly the same (or, if not used, a stateless [ReaDILoop] attribute can be used on one of them).
                    """ );
                return false;
            }
            return true;
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

        internal static ReaDIParameter? Create( IActivityMonitor monitor,
                                                GlobalTypeCache typeCache,
                                                ICachedType actualParameterType,
                                                ICachedType? loopStateType,
                                                CachedParameterInfo p )
        {
            var t = actualParameterType.Type;
            if( actualParameterType.EngineUnhandledType != EngineUnhandledType.None
                || actualParameterType == typeCache.KnownTypes.Object
                || t.IsEnum
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
                        Invalid [ReaDI] method parameter '{actualParameterType}' in '{p.Method.DeclaringType}': '{p.Method}'.
                        Parameter can only be interfaces or regular classes (and not object).
                        """ );
                }
                else
                {
                    monitor.Error( $"""
                        Invalid [ReaDI] method parameter '{p.ParameterType}' in '{p.Method.DeclaringType}': '{p.Method}'.
                        Type '{actualParameterType}' must be an interface or a regular classes (and not object).
                        """ );
                }
                return null;
            }
            return t.IsValueType || t.IsSealed
                    ? new Exact( actualParameterType, loopStateType, p )
                    : t.IsInterface
                        ? new VariantInterface( actualParameterType, loopStateType, p )
                        : new VariantClass( actualParameterType, loopStateType, p );
        }

    }

}
