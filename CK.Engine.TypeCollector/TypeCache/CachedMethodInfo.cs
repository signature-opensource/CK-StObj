using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CK.Engine.TypeCollector;

public sealed class CachedMethodInfo
{
    readonly ICachedType _declaringType;
    readonly MethodInfo _method;
    ImmutableArray<CachedParameterInfo> _parameterInfos;
    CachedParameterInfo? _returnParameterInfo;

    internal CachedMethodInfo( ICachedType declaringType, MethodInfo method )
    {
        _declaringType = declaringType;
        _method = method;
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

    public CachedParameterInfo ReturnParameterInfo => _returnParameterInfo ??= new CachedParameterInfo( this, _method.ReturnParameter );

    public StringBuilder Write( StringBuilder b )
    {
        if( _method.IsStatic ) b.Append( "static " );
        ReturnParameterInfo.Write( b );
        b.Append(' ').Append( Name ).Append('(');
        int i = 0;
        foreach( var p in ParameterInfos )
        {
            if( i++ > 0 ) b.Append( ',' );
            b.Append( ' ' );
            p.Write( b );
        }
        if( i > 0 ) b.Append( ' ' );
        b.Append( ')' );
        return b;
    }

    public override string ToString() => Write( new StringBuilder() ).ToString();
}
