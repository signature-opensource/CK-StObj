using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.AccessControl;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class HandlerType
    {
        readonly ICachedType _type;
        readonly LoopParameterType? _loopParameter;
        internal Callable? _firstCallable;
        IReaDIHandler? _currentHandler;

        HandlerType( ICachedType type, IReaDIHandler handler, LoopParameterType? loopParameter )
        {
            _type = type;
            _currentHandler = handler;
            _loopParameter = loopParameter;
        }

        public IReaDIHandler? CurrentHandler => _currentHandler;

        public ICachedType Type => _type;

        public Callable? FirstCallable => _firstCallable;

        [MemberNotNullWhen( true, nameof( LoopParameter ) )]
        public bool IsAlsoLoopParameter => _loopParameter != null;

        public LoopParameterType? LoopParameter => _loopParameter;

        internal static HandlerType? Create( IActivityMonitor monitor,
                                             ReaDIEngine engine,
                                             LoopTree loopTree,
                                             HandlerType? baseHandlerType,
                                             Dictionary<ICachedType, ParameterType> parameters,
                                             ICachedType type,
                                             IReaDIHandler initialHandler )
        {
            if( !loopTree.TryFindOrCreateFromHandlerType( monitor, type, out var loopParameter ) )
            {
                return null;
            }
            var handlerType = new HandlerType( type, initialHandler, loopParameter );
            if( !DiscoverReaDIMethods( monitor,
                                       engine,
                                       loopTree,
                                       parameters,
                                       type,
                                       handlerType ) )
            {
                return null;
            }
            return handlerType;

            static bool DiscoverReaDIMethods( IActivityMonitor monitor,
                                              ReaDIEngine engine,
                                              LoopTree loopTree,
                                              Dictionary<ICachedType, ParameterType> parameters,
                                              ICachedType type,
                                              HandlerType handlerType )
            {
                bool success = true;
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
                            success &= FindOrCreateParameters( monitor,
                                                               engine,
                                                               loopTree,
                                                               parameters,
                                                               callable,
                                                               parameterTypes,
                                                               out var isLoopCallable,
                                                               out var monitorIdx,
                                                               out var engineIdx );
                            if( success )
                            {
                                callable.Initialize( engine, monitorIdx, engineIdx, isLoopCallable );
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
                                                out bool isLoopCallable,
                                                out int monitorIdx,
                                                out int engineIdx )
            {
                isLoopCallable = false;
                monitorIdx = -1;
                engineIdx = -1;
                bool success = true;
                var parameterInfos = callable.Method.ParameterInfos;
                for( int i = 0; i < parameterInfos.Length; i++ )
                {
                    CachedParameterInfo paramInfo = parameterInfos[i];

                    var (parameterType, loopStateType) = ParameterType.GetLoopTypes( loopTree.TypeCache, paramInfo );

                    bool isLoopParam = loopStateType != null;

                    if( (parameterType == loopTree.IActivityMonitorType
                            && !CheckIntrinsicParameterType( monitor, callable, ref monitorIdx, i, isLoopParam, loopTree.IActivityMonitorType ))
                            ||
                            (parameterType == loopTree.ReaDIEngineType
                            && !CheckIntrinsicParameterType( monitor, callable, ref engineIdx, i, isLoopParam, loopTree.ReaDIEngineType )) )
                    {
                        success = false;
                        continue;
                    }
                    // If it's one of the intrinsic parameter type, it is useless to create a ParameterType for them.
                    // Note that IActivityMonitor contravariance is useless (it is the provided IActivityMonitor instance
                    // that is used) and that initrinsic types are errors in AddObject.
                    if( monitorIdx + engineIdx == -2 )
                    {
                        if( parameters.TryGetValue( parameterType, out var p ) )
                        {
                            if( !p.CheckLoopStateType( monitor, loopTree, loopStateType, paramInfo ) )
                            {
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
                            isLoopCallable = p.IsLoopParameter;
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

