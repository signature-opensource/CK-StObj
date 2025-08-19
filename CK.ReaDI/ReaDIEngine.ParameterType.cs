using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class ParameterType
    {
        readonly ICachedType _type;

        // This can be updated when a ReaDILoop<,> appears
        // and a stateless [ReaDI] parameter was registered.
        CachedParameterInfo _definer;
        internal LoopParameterType? _loopParameter;
        object? _currentValue;

        // Should we optimize this naïve implementation?
        readonly record struct Slot( Callable C, int Index );
        readonly List<Slot> _slots;

        ParameterType( ICachedType type, CachedParameterInfo definer )
        {
            _type = type;
            _definer = definer;
            _slots = [];
        }

        public ICachedType Type => _type;

        [MemberNotNullWhen( true, nameof( LoopParameter ) )]
        public bool IsLoopParameter => _loopParameter != null;

        public LoopParameterType? LoopParameter => _loopParameter;

        public bool SetCurrentValue( ReaDIEngine engine, object o )
        {
            if( _currentValue != null )
            {
                return false;
            }
            _currentValue = o;
            foreach( var (callable, index) in _slots )
            {
                callable.SetArgument( engine, index, o );
            }
            return true;
        }

        public bool CheckLoopStateType( IActivityMonitor monitor,
                                        LoopTree loopTree,
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
            Throw.DebugAssert( _loopParameter != null );
            // The current one was a stateless [ReaDI]. Types the state.
            if( thisLoopStateType == loopTree.VoidType )
            {
                _definer = p;
                _loopParameter.SetLoopStateType( loopStateType );
                return true;
            }
            // The new one is stateless. It's fine.
            if( loopStateType == loopTree.VoidType )
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
                    Loop state must be exactly the same (or, if the state is not used, a stateless [ReaDILoop] attribute can be used by any of them).
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
                                                LoopTree loopTree,
                                                ICachedType parameterType,
                                                CachedParameterInfo p )
        {
            var t = parameterType.Type;
            if( parameterType.EngineUnhandledType != EngineUnhandledType.None
                || parameterType == loopTree.TypeCache.KnownTypes.Object
                || t.IsValueType
                || !(t.IsInterface || t.IsClass)
                || t.IsByRef
                || t.IsByRefLike
                || t.IsArray
                || t.IsVariableBoundArray
                || parameterType.GenericTypeDefinition == loopTree.TypeCache.KnownTypes.GenericIEnumerableDefinition )
            {
                if( p.ParameterType == parameterType )
                {
                    monitor.Error( $"""
                        Invalid [ReaDI] method parameter '{parameterType.Name} {p.Name}' in '{p.Method.ToStringWithDeclaringType()}'.
                        Parameter can only be interfaces or regular classes (and not object).
                        """ );
                }
                else
                {
                    monitor.Error( $"""
                        Invalid [ReaDI] method parameter '{p.ParameterType.Name} {p.Name}' in '{p.Method.ToStringWithDeclaringType()}'.
                        Type '{parameterType}' must be an interface or a regular classes (and not object).
                        """ );
                }
                return null;
            }
            return new ParameterType( parameterType, p );
        }

        public override string ToString() => $"'{_definer.Name}' in '{_definer.Method}'";
    }
}

