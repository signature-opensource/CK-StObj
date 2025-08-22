using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        CachedParameter _definer;
        internal LoopParameterType? _loopParameter;
        object? _currentValue;

        // Should we optimize this naïve implementation?
        readonly record struct Slot( Callable C, int Index );
        readonly List<Slot> _slots;

        ParameterType( ICachedType type, CachedParameter definer, object? waitingObject )
        {
            _type = type;
            _definer = definer;
            _slots = [];
            _currentValue = waitingObject;
        }

        public ICachedType Type => _type;

        [MemberNotNullWhen( true, nameof( LoopParameter ) )]
        public bool IsLoopParameter => _loopParameter != null;

        public LoopParameterType? LoopParameter => _loopParameter;

        public object? CurrentValue => _currentValue;

        public CachedParameter Definer => _definer;

        public bool SetCurrentValue( IActivityMonitor monitor, ReaDIEngine engine, object o )
        {
            // Duplicate AddObject with the same instance is fine.
            if( _currentValue == o ) return true;
            if( _currentValue != null )
            {
                //if( engine.ActiveLoopParameter == _loopParameter )
                //{
                //    _loopParameter.PushNext( o );
                //    return true;
                //}
                monitor.Error( $"Duplicate Activation error: an instance of type '{_type}' is already available in the ReaDI engine." );
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
                                        CachedParameter p )
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
        internal static (ICachedType ParameterType, ICachedType? loopStateType) GetLoopTypes( GlobalTypeCache typeCache, CachedParameter p )
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
                                               GlobalTypeCache.WellKnownTypes wellknownTypes,
                                               ICachedType parameterType,
                                               CachedParameter p,
                                               object? initialValue )
        {
            var t = parameterType.Type;
            if( parameterType.EngineUnhandledType != EngineUnhandledType.None
                || parameterType == wellknownTypes.Object
                || t.IsValueType
                || !(t.IsInterface || t.IsClass)
                || t.IsByRef
                || t.IsByRefLike
                || t.IsArray
                || t.IsVariableBoundArray
                || parameterType.GenericTypeDefinition == wellknownTypes.GenericIEnumerableDefinition )
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
            
            return new ParameterType( parameterType, p, initialValue );
        }

        public override string ToString() => ToString( _slots.Count - 1, _definer );

        static string ToString( int moreCallableCount, CachedParameter definer )
        {
            var s = $"'{definer.Name}' in '{definer.Method}'";
            if( moreCallableCount > 0 )
            {
                s += $" (and {moreCallableCount} other methods)";
            }
            return s;
        }
    }
}

