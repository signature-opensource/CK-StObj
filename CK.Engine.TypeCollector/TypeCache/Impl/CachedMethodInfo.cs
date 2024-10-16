using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;

sealed class CachedMethodInfo : CachedMethodBase, ICachedMethodInfo
{
    CachedParameterInfo? _returnParameterInfo;

    internal CachedMethodInfo( ICachedType declaringType, MethodInfo method )
        : base( declaringType, method )
    {
    }

    public bool IsStatic => MethodInfo.IsStatic;

    public MethodInfo MethodInfo => Unsafe.As<MethodInfo>( _member );

    public CachedParameterInfo ReturnParameterInfo => _returnParameterInfo ??= new CachedParameterInfo( this, MethodInfo.ReturnParameter );

    public override StringBuilder Write( StringBuilder b )
    {
        if( MethodInfo.IsStatic ) b.Append( "static " );
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
