using CK.Engine.TypeCollector;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class LoopTree
    {
        readonly GlobalTypeCache _typeCache;
        readonly ICachedType _voidType;
        LoopParameterType? _firstChild;

        public LoopTree( GlobalTypeCache typeCache )
        {
            _typeCache = typeCache;
            _voidType = typeCache.KnownTypes.Void;
        }

        public GlobalTypeCache TypeCache => _typeCache;

        public ICachedType VoidType => _voidType;

        internal LoopParameterType? FindOrCreateFromNewParameter( IActivityMonitor monitor, ParameterType p, ICachedType loopStateType )
        {
            var result = _firstChild != null ? FindByType( _firstChild, p.Type ) : null;
            result ??= Create( monitor, p.Type, creator: p );
            if( result != null )
            {
                Throw.DebugAssert( "Just created or created via a child.", !result.HasParameter );
                Throw.DebugAssert( "It cannot have a loop state.", result.LoopStateType == _voidType );
                result.SetFirstParameter( p, loopStateType );
            }
            return result;
        }

        internal bool TryFindOrCreateFromHandlerType( IActivityMonitor monitor, ICachedType type, out LoopParameterType? loopParameter )
        {
            loopParameter = _firstChild != null ? FindByType( _firstChild, type ) : null;
            if( loopParameter == null )
            {
                LoopParameterType.GetLoopParameterAttributeValues( type, out bool isRoot, out Type? parentType );
                if( isRoot || parentType != null )
                {
                    loopParameter = DoCreateLoopParameterType( monitor, type, creator: null, isRoot, parentType );
                    if( loopParameter == null )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        static LoopParameterType? FindByType( [DisallowNull]LoopParameterType? first, ICachedType type )
        {
            while( first != null )
            {
                if( first.Type == type ) return first;
                var c = first._firstChild;
                if( c != null )
                {
                    c = FindByType( c, type );
                    if( c != null ) return c;
                }
                first = first._next;
            }
            return null;
        }

        //internal bool HandleNewLoopParameters( IActivityMonitor monitor, List<LoopParameterType> loopParameters )
        //{
        //    var root = loopParameters[0];
        //    if( root._tree == null )
        //    {
        //        // New root loop parameter type. 
        //        if( !LimitedStaticCheck( monitor, root.Parameter, _roots ) )
        //        {
        //            return false;
        //        }
        //        _roots.Add( root );
        //        root._tree = this;
        //    }
        //    // The first parameter type is located.
        //    // We must now process the remaining parameters to order
        //    // the loop parameters.
        //    for( int i = 0; i < loopParameters.Count; i++ )
        //    {
        //        LoopParameterType? p = loopParameters[i];
        //        if( p._tree != null )
        //        {
        //            // It must be below root.
        //            if( !p.IsBelow( root ) )
        //            {
        //                if( root.IsBelow( p ) )
        //                {
        //                    monitor.Error( "" );
        //                    return false;
        //                }

        //            }
        //        }
        //    }



        //    // Static type checking is limited. Two loop parameters MUST not be satisfied
        //    // by the same instance. The static type check is that they must have no common generalization
        //    // at all (Generalizations are Interfaces + BaseTypes as we only handle classes and interfaces).
        //    // This check is too strong: this prevents any template method pattern or unrelated useful interface
        //    // in loop parameter (even our own IReaDIHandler would be forbidden).
        //    // One may think that they must have no "instantiable" common generalization
        //    // is right but unfortunately, "instantiable" cannot be computed for an interface and even an abstract
        //    // class may eventually be implemented by code.Introducing an [Abstract] marker (the current [CKTypeDefiner])
        //    // may solve the issue, but even with this we must check the unicity at runtime: considering only the first
        //    // matching lopp parameter will introduce a possible random behavior.
        //    static bool LimitedStaticCheck( IActivityMonitor monitor, ParameterType p, IReadOnlyList<LoopParameterType> nodes )
        //    {
        //        bool success = true;
        //        var t = p.Type;
        //        foreach( var n in nodes )
        //        {
        //            var nT = n.Parameter.Type;
        //            if( AreRelated( t, nT, out var tFromN ) )
        //            {
        //                monitor.Error( $"""
        //                    Loop parameters' type must be independent:
        //                    parameter {n.Parameter} is assignable {(tFromN ? "from" : "to")}"
        //                    parameter {p} type. 
        //                    """ );
        //                success = false;
        //            }
        //            if( n.HasChildren )
        //            {
        //                success &= LimitedStaticCheck( monitor, p, n.Children );
        //            }
        //        }
        //        return success;

        //        static bool AreRelated( ICachedType tA, ICachedType tB, out bool aFromB )
        //        {
        //            Throw.DebugAssert( tB != tA );
        //            aFromB = false;
        //            int cmp = tA.TypeDepth - tB.TypeDepth;
        //            if( cmp > 0 )
        //            {
        //                return IsAbove( tA, tB );
        //            }
        //            else if( cmp < 0 )
        //            {
        //                aFromB = true;
        //                return IsAbove( tB, tA );
        //            }
        //            return false;

        //            static bool IsAbove( ICachedType t, ICachedType below )
        //            {
        //                if( t.Type.IsInterface )
        //                {
        //                    return below.Interfaces.Contains( t );
        //                }
        //                Throw.DebugAssert( t.Type.IsClass );
        //                var b = below.BaseType;
        //                while( b != null )
        //                {
        //                    if( b == t ) return true;
        //                    b = b.BaseType;
        //                }
        //                return false;
        //            }

        //        }

        //    }
        //}

        LoopParameterType? Create( IActivityMonitor monitor, ICachedType type, object creator )
        {
            Throw.DebugAssert( creator is ParameterType or ICachedType );
            LoopParameterType.GetLoopParameterAttributeValues( type, out bool isRoot, out System.Type? parentType );
            return DoCreateLoopParameterType( monitor, type, creator, isRoot, parentType );
        }

        LoopParameterType? DoCreateLoopParameterType( IActivityMonitor monitor, ICachedType type, object? creator, bool isRoot, Type? parentType )
        {
            if( isRoot )
            {
                if( parentType != null )
                {
                    monitor.Error( $"Type '{type}' cannot have both [ReaDILoopRootParameter] and [ReaDILoopParameter<>]." );
                    return null;
                }
                var newRoot = new LoopParameterType( this, type, parent: null );
                newRoot._next = _firstChild;
                _firstChild = newRoot;
                return newRoot;
            }
            else if( parentType == null )
            {
                if( creator is ParameterType p )
                {
                    monitor.Error( $"Type '{type}' must be decorated with [ReaDILoopRootParameter] or [ReaDILoopParameter<>] because " +
                                   $"it is referenced by parameter {p}." );
                }
                else
                {
                    Throw.DebugAssert( "When creating from a new HandlerType, isRoot is true or parentType is not null.",
                                       creator is ICachedType );
                    monitor.Error( $"Type '{type}' must be decorated with [ReaDILoopRootParameter] or [ReaDILoopParameter<>] because " +
                                   $"it is referenced by [ReaDILoopParameter<{type.Name}>] of {creator}." );
                }
                return null;
            }
            var tParent = _typeCache.Get( parentType );
            var nParent = _firstChild != null ? FindByType( _firstChild, tParent ) : null;
            nParent ??= Create( monitor, tParent, creator: type );
            return nParent == null
                    ? null
                    : new LoopParameterType( this, type, nParent );
        }

    }
}

