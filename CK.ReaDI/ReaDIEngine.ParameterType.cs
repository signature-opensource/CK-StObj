using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    abstract class ParameterType
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

        internal ParameterType? _nextActiveDescriptor;
        internal ParameterType? _prevActiveDescriptor;

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
            public Exact( ICachedType type, CachedParameterInfo loopDefiner )
                : base( type, loopDefiner )
            {
                Throw.DebugAssert( type.Type.IsSealed );
            }

            protected override bool ContravariantMatch( ICachedType o ) => Throw.NotSupportedException<bool>();
        }

        sealed class VariantClass : ParameterType
        {
            public VariantClass( ICachedType type, CachedParameterInfo loopDefiner )
                : base( type, loopDefiner )
            {
                Throw.DebugAssert( !type.Type.IsValueType && !type.Type.IsSealed && !type.Type.IsInterface );
            }

            protected override bool ContravariantMatch( ICachedType o )
            {
                if( _type.TypeDepth < o.TypeDepth )
                {
                    var b = o.BaseType;
                    while( b != null )
                    {
                        if( _type == b ) return true;
                        b = b.BaseType;
                    }
                }
                return false;
            }
        }

        sealed class VariantInterface : ParameterType
        {
            public VariantInterface( ICachedType type, CachedParameterInfo loopDefiner )
                : base( type, loopDefiner )
            {
                Throw.DebugAssert( type.Type.IsInterface );
            }

            protected override bool ContravariantMatch( ICachedType o ) => _type.TypeDepth < o.TypeDepth && o.Interfaces.Contains( _type );
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
            return t.IsSealed
                    ? new Exact( parameterType, p )
                    : t.IsInterface
                        ? new VariantInterface( parameterType, p )
                        : new VariantClass( parameterType, p );
        }

        public override string ToString() => $"'{_definer.Name}' in '{_definer.Method}'";
    }
}

