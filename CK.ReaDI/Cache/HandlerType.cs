using CK.Core;
using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core;

public sealed class HandlerType
{
    readonly ICachedType _type;
    internal CallableType? _firstCallable;
    
    HandlerType( ICachedType type )
    {
        _type = type;
    }

    public ICachedType Type => _type;

    public CallableType? FirstCallable => _firstCallable;

    internal static HandlerType? Create( IActivityMonitor monitor,
                                         ReaDIEngine engine,
                                         Dictionary<ICachedType, ParameterType> parameters,
                                         ICachedType type )
    {
        var handlerType = new HandlerType( type );
        if( !DiscoverReaDIMethods( monitor,
                                   engine,
                                   parameters,
                                   type,
                                   handlerType ) )
        {
            return null;
        }
        return handlerType;

        static bool DiscoverReaDIMethods( IActivityMonitor monitor,
                                          ReaDICache cache,
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
                        var callable = new CallableType( handlerType, m, parameterTypes );
                        success &= FindOrCreateParameters( monitor,
                                                           cache,
                                                           parameters,
                                                           callable,
                                                           parameterTypes,
                                                           out var monitorIdx,
                                                           out var engineIdx );
                        if( success )
                        {
                            callable.Initialize( monitorIdx, engineIdx );
                        }
                    }
                }
            }

            return success;
        }

        static bool FindOrCreateParameters( IActivityMonitor monitor,
                                            ReaDICache cache,
                                            Dictionary<ICachedType, ParameterType> parameters,
                                            CallableType callable,
                                            ParameterType[] parameterTypes,
                                            out int monitorIdx,
                                            out int engineIdx )
        {
            monitorIdx = -1;
            engineIdx = -1;
            bool success = true;
            var parameterInfos = callable.Method.ParameterInfos;
            for( int i = 0; i < parameterInfos.Length; i++ )
            {
                CachedParameter paramInfo = parameterInfos[i];

                var parameterType = paramInfo.ParameterType;

                bool isIntrinsicParam = false;
                if( parameterType == cache.IActivityMonitorType )
                {
                    if( !CheckIntrinsicParameterType( monitor, callable, ref monitorIdx, i, cache.IActivityMonitorType ) )
                    {
                        success = false;
                        continue;
                    }
                    isIntrinsicParam = true;
                }
                else if( parameterType == cache.ReaDIEngineType )
                {
                    if( !CheckIntrinsicParameterType( monitor, callable, ref engineIdx, i, cache.ReaDIEngineType ) )
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
                            p = ParameterType.Create( monitor, cache.TypeCache.KnownTypes, parameterType, paramInfo );
                            if( p == null )
                            {
                                success = false;
                            }
                            else
                            {
                                parameters.Add( parameterType, p );
                            }
                        }
                    }
                    if( success )
                    {
                        Throw.DebugAssert( p != null );
                        parameterTypes[i] = p;
                    }
                }
            }
            return success;

            static bool CheckIntrinsicParameterType( IActivityMonitor monitor,
                                                     CallableType callable,
                                                     ref int knownIdx,
                                                     int idx,
                                                     ICachedType knownType )
            {
                if( knownIdx >= 0 )
                {
                    monitor.Error( $"{knownType.Name} can appear at most once in '{callable.Method}'." );
                    return false;
                }
                knownIdx = idx;
                return true;
            }
        }

    }
}
