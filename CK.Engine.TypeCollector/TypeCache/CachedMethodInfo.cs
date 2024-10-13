using System;
using System.Collections.Immutable;
using System.Reflection;

namespace CK.Engine.TypeCollector;

public sealed class CachedMethodInfo
{
    readonly ICachedType _declaringType;
    readonly MethodInfo _method;
    readonly Type _returnType;
    ImmutableArray<CachedParameterInfo> _parameterInfos;

    internal CachedMethodInfo( ICachedType declaringType, MethodInfo method )
    {
        _declaringType = declaringType;
        _method = method;
        _returnType = method.ReturnType;
    }

    /// <summary>
    /// Gets whether this is a static method.
    /// </summary>
    public bool IsStatic => _method.IsStatic;

    /// <summary>
    /// Gets whether this method is public.
    /// </summary>
    public bool IsPublic => _method.IsPublic;

    /// <summary>
    /// Gets the name of the method. May be a special name (like 'get_XXX').
    /// </summary>
    public string Name => _method.Name;

    /// <summary>
    /// Gets the declaring type.
    /// </summary>
    public ICachedType DeclaringType => _declaringType;

    /// <summary>
    /// Gets the cached info. Should rarely be used directly.
    /// </summary>
    public MethodInfo MethodInfo => _method;

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public ImmutableArray<CachedParameterInfo> ParameterInfos
    {
        get
        {
            if( _parameterInfos.IsDefault )
            {
                var parameters = _method.GetParameters();
                var b = ImmutableArray.CreateBuilder<CachedParameterInfo>( parameters.Length );
                foreach( var p in parameters ) b.Add( new CachedParameterInfo( this, p ) );
                _parameterInfos = b.MoveToImmutable();
            }
            return _parameterInfos;
        }
    }
}
