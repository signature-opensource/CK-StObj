using CK.Engine.TypeCollector;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core;

public sealed partial class ReaDIEngine
{
    sealed class LoopContext : IReaDIContext
    {
        readonly ReaDIEngine _engine;
        readonly LoopContext? _parent;
        readonly List<(ICachedType Type, object Value)> _objects;
        List<CallableHost>? _handlers;

        public LoopContext( ReaDIEngine engine, LoopContext? parent )
        {
            _engine = engine;
            _parent = parent;
            _objects = new List<(ICachedType, object)>();
        }

        internal LoopContext? Parent => _parent;

        internal List<(ICachedType,object)> Objects => _objects;

        public bool AddObject( IActivityMonitor monitor, object o )
        {
            if( !_engine.AddObject( monitor, o, out var oT ) )
            {
                return false;
            }
            if( o is IReaDIHandler h && !AddHandler( monitor, oT, h ) )
            {
                return false;
            }
            _objects.Add( (oT, o) );
            return true;
        }

        bool AddHandler( IActivityMonitor monitor, ICachedType oT, IReaDIHandler h )
        {
            bool success = true;
            var host = new CallableHost( h );
            foreach( var m in oT.DeclaredMembers.OfType<ICachedMethodInfo>() )
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
                        var callable = host.AddCallable( m );
                        for( int i = 0; i < m.ParameterInfos.Length; i++ )
                        {
                            CachedParameterInfo p = m.ParameterInfos[i];
                            var (parameterType, loopStateType) = ParameterType.GetLoopTypes( _typeCache, p );
                            if( _reaDIParameters.TryGetValue( parameterType, out var pDesc ) )
                            {
                                if( !pDesc.CheckLoopStateType( monitor, _typeCache, loopStateType, p ) )
                                {
                                    success = false;
                                    continue;
                                }
                                pDesc.AddCallableParameter( this, callable, i );
                                if( pDesc.Value != null )
                                {
                                    callable.SetArgument( this, i, pDesc.Value );
                                }
                            }
                            else
                            {
                                pDesc = ParameterType.Create( monitor, _typeCache, parameterType, loopStateType, p );
                                if( pDesc == null )
                                {
                                    success = false;
                                    continue;
                                }
                                _reaDIParameters.Add( parameterType, pDesc );
                            }
                            if( success )
                            {
                                pDesc.AddCallableParameter( this, callable, i );
                                object? available = pDesc.Value;
                                if( available == null )
                                {
                                    var c = _currentContext;
                                    do
                                    {
                                        foreach( var (eoT, eo) in c.Objects )
                                        {
                                            if( !pDesc.OnObjectAppear( monitor, this, eoT, eo, out var matched ) )
                                            {
                                                return SetError( monitor );
                                            }
                                            if( matched )
                                            {
                                                available = eo;
                                            }
                                        }
                                        c = c.Parent;
                                    }
                                    while( c != null );
                                    return true;
                                }

                            }
                        }
                        if( success && !callable.IsWaiting )
                        {
                            AddReadyToRun( callable );
                        }
                    }
                }
            }
            if( !success )
            {
                SetError( monitor );
            }
            return success;
        }

    }

}
