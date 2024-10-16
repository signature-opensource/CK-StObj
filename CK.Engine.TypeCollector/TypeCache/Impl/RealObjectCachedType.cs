using CK.Core;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace CK.Engine.TypeCollector;

sealed class RealObjectCachedType : CachedType, IRealObjectCachedType
{
    ImmutableArray<ICachedMethodInfo> _configureMethods;
    ICachedMethodInfo? _requiresMethod;
    bool _methodsSuccess;

    internal RealObjectCachedType( GlobalTypeCache cache,
                                   Type type,
                                   int typeDepth,
                                   CachedAssembly assembly,
                                   ImmutableArray<ICachedType> interfaces,
                                   ICachedType? baseType )
        : base( cache, type, typeDepth, assembly, interfaces, baseType )
    {
    }

    public bool TryGetRequiresMethod( IActivityMonitor monitor, out ICachedMethodInfo? requiresMethod )
    {
        if( _requiresMethod == null ) GetRealObjectMethods( monitor );
        requiresMethod = _requiresMethod;
        return _methodsSuccess;
    }

    public bool TryGetConfigureMethods( IActivityMonitor monitor, out ImmutableArray<ICachedMethodInfo> configureMethods )
    {
        if( _configureMethods.IsDefault ) GetRealObjectMethods( monitor );
        configureMethods = _configureMethods;
        return _methodsSuccess;
    }

    void GetRealObjectMethods( IActivityMonitor monitor )
    {
        bool success = true;
        ImmutableArray<ICachedMethodInfo>.Builder? bConfigure = null;
        ICachedMethodInfo? requires = null;
        foreach( var m in DeclaredMembers )
        {
            if( m is not CachedMethodInfo method ) continue;
            switch( m.Name )
            {
                case "Configure":
                    success &= GetConfigure( monitor, method, ref bConfigure );
                    break;
                case "Requires":
                    success &= GetRequires( monitor, method, ref requires );
                    break;
            }
        }
        _methodsSuccess = success;
        _configureMethods = bConfigure != null ? bConfigure.DrainToImmutable() : ImmutableArray<ICachedMethodInfo>.Empty;
        _requiresMethod = requires;
    }

    bool GetRequires( IActivityMonitor monitor, ICachedMethodInfo method, ref ICachedMethodInfo? requires )
    {
        if( !WarnOnStatic( monitor, method, "Requires" ) ) return true;
        if( !ErrorOnPrivateOrNonVoid( monitor, method, "Requires" ) ) return false;
        if( requires != null )
        {
            monitor.Error( $"Type '{ToString}' declares '{requires}' and '{method}' methods. Requires method must be declared only once." );
            return false;
        }
        var p = method.ParameterInfos;
        if( p.Length == 0 )
        {
            monitor.Warn( $"Method 'public static {ToString()}.Requires()' has no parameter. It is ignored." );
            return true;
        }
        if( p[0].ParameterInfo.ParameterType != Type )
        {
            monitor.Error( $"Invalid method '{method}': the first parameter must be '{Type.Name} instance'." );
            return false;
        }
        if( p.Length == 1 )
        {
            monitor.Warn( $"Method '{method}' doesn't declare any required Real Object types. It is ignored." );
            return true;
        }
        var notGood = p.Skip(1).Where( p => p.ParameterType is not RealObjectCachedType );
        if( notGood.Any() )
        {
            monitor.Error( $"Invalid method '{method}': parameter '{notGood.Select( p => p.ToString()).Concatenate("', '")}' must be IRealObject." );
            return false;
        }
        requires = method;
        return true;
    }

    bool GetConfigure( IActivityMonitor monitor, CachedMethodInfo method, ref ImmutableArray<ICachedMethodInfo>.Builder? bConfigure )
    {
        if( !WarnOnStatic( monitor, method, "Configure" ) ) return true;
        if( !ErrorOnPrivateOrNonVoid( monitor, method, "Configure" ) ) return false;
        var p = method.ParameterInfos;
        if( p.Length < 3
            || p[0].ParameterInfo.ParameterType != typeof( IActivityMonitor )
            || p[2].ParameterInfo.ParameterType != Type )
        {
            monitor.Error( $$"""
                            Invalid method 'public static {{ToString()}}.Configure' signature. Expected:
                            public static void Configure( IActivityMonitor monitor, ConfiguredType configureTarget, {declaringType.CSharpName} instance, ... )

                            Where ... can be services registered by RegisterStartupServices methods.
                            """ );
            return false;
        }
        bConfigure ??= ImmutableArray.CreateBuilder<ICachedMethodInfo>();
        bConfigure.Add( method );
        return true;
    }

    private bool ErrorOnPrivateOrNonVoid( IActivityMonitor monitor, ICachedMethodInfo method, string name )
    {
        if( !method.IsPublic || method.MethodInfo.ReturnType != typeof( void ) )
        {
            monitor.Error( $"Static method '{ToString()}.{name}' must be public and return void (or should not be named '{name}')." );
            return false;
        }
        return true;
    }

    private static bool WarnOnStatic( IActivityMonitor monitor, ICachedMethodInfo method, string name )
    {
        if( !method.IsStatic )
        {
            monitor.Warn( $"Method '{method}' is not static. It is not considered as a RealObject {name} method." );
            return false;
        }
        return true;
    }
}
