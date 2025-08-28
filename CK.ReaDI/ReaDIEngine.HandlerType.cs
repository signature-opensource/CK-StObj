using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{

    sealed class HandlerType
    {
        readonly ICachedType _type;
        readonly LoopParameterType? _loopParameter;
        readonly SourcedType? _initialSourcedType;
        internal Callable? _firstCallable;
        object? _currentHandler;

        HandlerType( ICachedType type, IReaDIHandler handler, SourcedType? sourcedType, LoopParameterType? loopParameter )
        {
            _type = type;
            _initialSourcedType = sourcedType;
            _loopParameter = loopParameter;
            if( sourcedType == null )
            {
                _currentHandler = handler;
            }
            else
            {
                _currentHandler = new SourcedHandlerInstance( handler, sourcedType, null );
            }
        }

        public IReaDIHandler? CurrentHandler => _currentHandler as IReaDIHandler;

        public SourcedHandlerInstance? FirstSourcedHandler => _currentHandler as SourcedHandlerInstance;

        public ICachedType Type => _type;

        public Callable? FirstCallable => _firstCallable;

        [MemberNotNullWhen( true, nameof( LoopParameter ) )]
        public bool IsAlsoLoopParameter => _loopParameter != null;

        public LoopParameterType? LoopParameter => _loopParameter;

        [MemberNotNullWhen( true, nameof( InitialSourceType ) )]
        public bool IsFromSourceType => _initialSourcedType != null;

        public ICachedType? InitialSourceType => _initialSourcedType?.SourceType;

        public SourcedHandlerInstance AddSourceInstance( SourcedType sourcedType, IReaDIHandler handler )
        {
            Throw.DebugAssert( _initialSourcedType != null );
            Throw.DebugAssert( _currentHandler is SourcedHandlerInstance );
            var h = new SourcedHandlerInstance( handler, sourcedType, FirstSourcedHandler );
            _currentHandler = h;
            return h;
        }

        internal void ReverseCallableList()
        {
            var c = _firstCallable;
            if( c != null && c._next != null )
            {
                Callable? previous = null;
                Callable? next = c;
                while( c != null )
                {
                    next = c._next;
                    c._next = previous;
                    previous = c;
                    c = next;
                }
                _firstCallable = previous;
            }
        }

        internal static HandlerType? Create( IActivityMonitor monitor,
                                             ReaDIEngine engine,
                                             LoopTree loopTree,
                                             Dictionary<ICachedType, ParameterType> parameters,
                                             ICachedType type,
                                             SourcedType? sourceType,
                                             IReaDIHandler initialHandler )
        {
            if( !loopTree.TryFindOrCreateFromHandlerType( monitor, type, out var loopParameter ) )
            {
                return null;
            }
            if( loopParameter != null && sourceType != null )
            {
                var msg = loopParameter.HasParameter
                            ? $"Type '{type.Name}' is declared by {loopParameter.Parameter}."
                            : $"Type '{type.Name}' is considered a loop parameter because it is decorated with [HierarchicalTypeRoot] or [HierarchicalType<>].";
                monitor.Error( $"""
                    ReaDIHandler '{type}' registered from engine attributes on type '{sourceType}' cannot be a loop parameter type.
                    {msg}
                    """ );
                return null;
            }
            var handlerType = new HandlerType( type, initialHandler, sourceType, loopParameter );
            if( !DiscoverReaDIMethods( monitor,
                                       engine,
                                       loopTree,
                                       parameters,
                                       type,
                                       handlerType ) )
            {
                return null;
            }
            handlerType.ReverseCallableList();
            return handlerType;

            static bool DiscoverReaDIMethods( IActivityMonitor monitor,
                                              ReaDIEngine engine,
                                              LoopTree loopTree,
                                              Dictionary<ICachedType, ParameterType> parameters,
                                              ICachedType type,
                                              HandlerType handlerType )
            {
                bool success = true;
                foreach( var m in type.Members.OfType<CachedMethod>() )
                {
                    if( !m.MethodInfo.IsSpecialName && m.AttributesData.Any( a => a.AttributeType == typeof( ReaDIAttribute ) ) )
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
                            success &= FindOrCreateParameters( monitor,
                                                               engine,
                                                               loopTree,
                                                               parameters,
                                                               callable,
                                                               parameterTypes,
                                                               out var deepestLoop,
                                                               out var monitorIdx,
                                                               out var engineIdx );
                            if( success )
                            {
                                // We call Initialize immediately (because we have the required parameters).
                                // This pushes the callable if it is ready to run.
                                // We could not do this here but wait for the revert of the final list.
                                // Pushing in Initialize avoids an other method on the Callable (and respects
                                // the final Callable ordering.
                                callable.Initialize( engine, monitorIdx, engineIdx, deepestLoop );
                            }
                        }
                    }
                }

                return success;
            }

            static bool FindOrCreateParameters( IActivityMonitor monitor,
                                                ReaDIEngine engine,
                                                LoopTree loopTree,
                                                Dictionary<ICachedType, ParameterType> parameters,
                                                Callable callable,
                                                ParameterType[] parameterTypes,
                                                out LoopParameterType? deepestLoop,
                                                out int monitorIdx,
                                                out int engineIdx )
            {
                deepestLoop = null;
                monitorIdx = -1;
                engineIdx = -1;
                bool success = true;
                var parameterInfos = callable.Method.ParameterInfos;
                for( int i = 0; i < parameterInfos.Length; i++ )
                {
                    CachedParameter paramInfo = parameterInfos[i];

                    var (parameterType, loopStateType) = ParameterType.GetLoopTypes( loopTree.TypeCache, paramInfo );

                    bool isLoopParam = loopStateType != null;
                    bool isIntrinsicParam = false;
                    if( parameterType == loopTree.IActivityMonitorType )
                    {
                        if( !CheckIntrinsicParameterType( monitor, callable, ref monitorIdx, i, isLoopParam, loopTree.IActivityMonitorType ) )
                        {
                            success = false;
                            continue;
                        }
                        isIntrinsicParam = true;
                    }
                    else if( parameterType == loopTree.ReaDIEngineType )
                    {
                        if( !CheckIntrinsicParameterType( monitor, callable, ref engineIdx, i, isLoopParam, loopTree.ReaDIEngineType ) )
                        {
                            success = false;
                            continue;
                        }
                        isIntrinsicParam = true;
                    }
                    // If it's one of the intrinsic parameter type, it is useless to create a ParameterType for them.
                    // Note that IActivityMonitor contravariance is useless (it is the provided IActivityMonitor instance
                    // that is used) and that intrinsic types are errors in AddObject.
                    if( !isIntrinsicParam )
                    {
                        if( parameters.TryGetValue( parameterType, out var p ) )
                        {
                            if( !p.CheckLoopStateType( monitor, loopTree, loopStateType, paramInfo ) )
                            {
                                success = false;
                            }
                            var idx = Array.IndexOf( parameterTypes, p, 0, i );
                            if( idx >= 0 )
                            {
                                monitor.Error( $"Duplicate parameter types in '{callable.Method}': '{paramInfo.Name}' and '{parameterInfos[idx].Name}' are both '{parameterType}'." );
                                success = false;
                            }
                        }
                        else
                        {
                            // "Family unicity" check.
                            foreach( var other in parameters.Values )
                            {
                                if( other.Type.ConcreteGeneralizations.Overlaps( parameterType.ConcreteGeneralizations ) )
                                {
                                    var common = other.Type.ConcreteGeneralizations.Intersect( parameterType.ConcreteGeneralizations )
                                                      .Select( c => c.CSharpName );
                                    monitor.Error( $"""
                                        Conflicting parameter '{paramInfo.Name}' in {callable.Method}: existing parameter type '{other.Type}' intersects it.
                                        [ReaDI] parameter types must be independent from each others. Common abstractions are:
                                        '{common.Concatenate( "', '" )}'.
                                        """ );
                                    success = false;
                                }
                            }
                            if( success )
                            {
                                var initialValue = i == monitorIdx
                                               ? loopTree.IActivityMonitorType
                                               : i == engineIdx
                                               ? engine
                                               : (object?)null;
                                if( initialValue == null )
                                {
                                    success &= engine.FindWaitingObjectFor( monitor, parameterType, paramInfo, out initialValue );
                                }
                                p = ParameterType.Create( monitor, loopTree.TypeCache.KnownTypes, parameterType, paramInfo, initialValue );
                                if( p == null )
                                {
                                    success = false;
                                }
                                else
                                {
                                    parameters.Add( parameterType, p );
                                    if( isLoopParam )
                                    {
                                        Throw.DebugAssert( loopStateType != null );
                                        var loopParam = loopTree.FindOrCreateFromNewParameter( monitor, p, loopStateType );
                                        if( loopParam == null )
                                        {
                                            success = false;
                                        }
                                        p._loopParameter = loopParam;
                                    }
                                }
                            }
                        }
                        if( success )
                        {
                            Throw.DebugAssert( p != null );
                            if( p.IsLoopParameter )
                            {
                                if( deepestLoop == null )
                                {
                                    deepestLoop = p.LoopParameter;
                                }
                                else
                                {
                                    if( deepestLoop.Type.HierarchicalTypePath[0] != parameterType.HierarchicalTypePath[0] )
                                    {
                                        monitor.Error( $"""
                                            Invalid loop parameters:
                                            {deepestLoop} is subordinated to root type '{deepestLoop.Type.HierarchicalTypePath[0]}',
                                            and '{paramInfo.Name}' is subordinated to root type '{parameterType.HierarchicalTypePath[0]}'.
                                            Cross-product looping is not supported.
                                            """ );
                                        success = false;
                                    }
                                    if( parameterType.HierarchicalTypePath.Length > deepestLoop.Type.HierarchicalTypePath.Length )
                                    {
                                        deepestLoop = p.LoopParameter;
                                    }
                                }
                            }
                            parameterTypes[i] = p;
                            p.AddCallableParameter( callable, i );
                        }
                    }
                }
                return success;

                static bool CheckIntrinsicParameterType( IActivityMonitor monitor,
                                                     Callable callable,
                                                     ref int knownIdx,
                                                     int idx,
                                                     bool hasLoopAttribute,
                                                     ICachedType knownType )
                {
                    if( knownIdx >= 0 )
                    {
                        monitor.Error( $"{knownType.Name} must can appear at most once is '{callable.Method}'." );
                        return false;
                    }
                    knownIdx = idx;
                    if( hasLoopAttribute )
                    {
                        monitor.Error( $"{knownType.Name} cannot be a ReaDILoop parameter in '{callable.Method}'." );
                        return false;
                    }
                    return true;
                }
            }

        }
    }

}

