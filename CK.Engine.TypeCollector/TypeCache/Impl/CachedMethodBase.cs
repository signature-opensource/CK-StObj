using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;

abstract class CachedMethodBase : CachedMemberInfo, ICachedMethodBase
{
    ImmutableArray<CachedParameterInfo> _parameterInfos;

    internal CachedMethodBase( ICachedType declaringType, MethodBase method )
        : base( declaringType, method )
    {
    }

    /// <summary>
    /// Gets the cached info. Should rarely be used directly.
    /// </summary>
    public MethodBase MethodBase => Unsafe.As<MethodBase>( _member );

    public bool IsPublic => MethodBase.IsPublic;

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public ImmutableArray<CachedParameterInfo> ParameterInfos
    {
        get
        {
            if( _parameterInfos.IsDefault )
            {
                var parameters = MethodBase.GetParameters();
                var b = ImmutableArray.CreateBuilder<CachedParameterInfo>( parameters.Length );
                foreach( var p in parameters ) b.Add( new CachedParameterInfo( this, p ) );
                _parameterInfos = b.MoveToImmutable();
            }
            return _parameterInfos;
        }
    }

    public void WriteParameters( StringBuilder b )
    {
        b.Append( '(' );
        int i = 0;
        foreach( var p in ParameterInfos )
        {
            if( i++ > 0 ) b.Append( ',' );
            b.Append( ' ' );
            p.Write( b );
        }
        if( i > 0 ) b.Append( ' ' );
        b.Append( ')' );
    }
}
