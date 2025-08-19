using CK.Engine.TypeCollector;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class HandlerType
    {
        readonly ICachedType _type;
        internal Callable? _firstCallable;
        IReaDIHandler? _currentHandler;

        HandlerType( ICachedType type )
        {
            _type = type;
        }

        public IReaDIHandler? CurrentHandler => _currentHandler;

        public ICachedType Type => _type;

        public Callable? FirstCallable => _firstCallable;

        internal static HandlerType? Create( IActivityMonitor monitor,
                                             LoopTree loopTree,
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
                        success &= FindOrCreateParameters( monitor, loopTree, parameters, callable, parameterTypes, out var isLoopCallable );
                    }
                }
            }
            return success ? handlerType : null;

            static bool FindOrCreateParameters( IActivityMonitor monitor,
                                                LoopTree loopTree,
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
                    var (parameterType, loopStateType) = ParameterType.GetLoopTypes( loopTree.TypeCache, paramInfo );
                    if( parameters.TryGetValue( parameterType, out var p ) )
                    {
                        if( p.CheckLoopStateType( monitor, loopTree, loopStateType, paramInfo ) )
                        {
                            success = false;
                        }
                    }
                    else
                    {
                        p = ParameterType.Create( monitor, loopTree, parameterType, paramInfo );
                        if( p != null )
                        {
                            if( loopStateType != null )
                            {
                                var loopParam = loopTree.FindOrCreateFromNewParameter( monitor, p, loopStateType );
                                if( loopParam == null )
                                {
                                    success = false;
                                }
                                p._loopParameter = loopParam;
                            }
                        }
                    }
                    if( !success )
                    {
                        continue;
                    }
                    Throw.DebugAssert( p != null );
                    isLoopCallable = p.IsLoopParameter;
                    parameters.Add( parameterType, p );
                    parameterTypes[i] = p;
                    p.AddCallableParameter( callable, i );
                }
                return success;
            }
        }
    }
}

