using CK.Engine.TypeCollector;
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
                                             Dictionary<ICachedType, ParameterType> parameters,
                                             ICachedType type,
                                             IReaDIHandler initialHandler )
        {
            if( !loopTree.TryFindOrCreateFromHandlerType( monitor, type, out var loopParameter ) )
            {
                return null;
            }
            bool success = true;
            var handlerType = new HandlerType( type, initialHandler, loopParameter );
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
            return success ? handlerType : null;

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
                            && !CheckKnownParameterType( monitor, callable, ref monitorIdx, i, isLoopParam, loopTree.IActivityMonitorType ))
                            ||
                            (parameterType == loopTree.ReaDIEngineType
                            && !CheckKnownParameterType( monitor, callable, ref engineIdx, i, isLoopParam, loopTree.ReaDIEngineType )) )
                    {
                        success = false;
                        continue;
                    }

                    if( parameters.TryGetValue( parameterType, out var p ) )
                    {
                        if( !p.CheckLoopStateType( monitor, loopTree, loopStateType, paramInfo ) )
                        {
                            success = false;
                        }
                    }
                    else
                    {
                        var initialValue = i == monitorIdx
                                           ? loopTree.IActivityMonitorType
                                           : i == engineIdx
                                           ? engine
                                           // No contravariance for the moment.
                                           : engine._waitingObjects.GetValueOrDefault( parameterType );

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
                    if( success )
                    {
                        Throw.DebugAssert( p != null );
                        isLoopCallable = p.IsLoopParameter;
                        parameterTypes[i] = p;
                        p.AddCallableParameter( callable, i );
                    }
                }
                return success;

                static bool CheckKnownParameterType( IActivityMonitor monitor,
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

