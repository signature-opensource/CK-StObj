using CK.Core;
using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Configure method description.
/// <para>
/// a Configure method is a public static method with at least 3 parameters:
/// <code>
/// public static Configure( IActivityMonitor monitor, ConfiguredType configureTarget, RealObjectType instance ) { ... }
/// </code>
/// The <c>ConfigureType</c> is typically the <c>IServiceCollection</c>, the <c>IHostApplicationBuilder</c>, the <c>HostApplicationBuilder</c>
/// or the <c>WebApplicationBuilder</c>.
/// </para>
/// </summary>
public sealed class RealObjectConfigureMethod
{
    readonly MethodInfo _method;
    readonly ImmutableArray<ParameterInfo> _parameters;

    RealObjectConfigureMethod( MethodInfo m, ImmutableArray<ParameterInfo> parameters )
    {
        _method = m;
        _parameters = parameters;
    }

    public MethodInfo Method => _method;

    public Type ConfiguredType => _parameters[1].ParameterType;

    internal static RealObjectConfigureMethod? TryCreate( IActivityMonitor monitor, ICachedType declaringType, MethodInfo method )
    {
        Throw.DebugAssert( method.IsStatic && method.Name == "Configure" );
        if( !method.IsPublic || method.ReturnType != typeof( void ) )
        {
            monitor.Error( $"Static method '{declaringType.CSharpName}.Configure' must be public and return void (or should not be named Configure)." );
            return null;
        }
        var p = ImmutableCollectionsMarshal.AsImmutableArray( method.GetParameters() );
        if( p.Length < 3
            || p[0].ParameterType != typeof( IActivityMonitor )
            || p[2].ParameterType != method.DeclaringType )
        {
            monitor.Error( $$"""
                            Invalid method 'public static {declaringType.CSharpName}.Configure' signature. Expected:
                            public static {declaringType.CSharpName}.Configure( IActivityMonitor monitor, ConfiguredType configureTarget, {declaringType.CSharpName} instance, ... )

                            Where ... can be services registered by RegisterStartupServices methods.
                            """ );
            return null;
        }
        return new RealObjectConfigureMethod( method, p );
    }
}
