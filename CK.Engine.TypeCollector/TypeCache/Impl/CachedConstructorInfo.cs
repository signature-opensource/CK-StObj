using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;

sealed class CachedConstructorInfo : CachedMethodBase, ICachedConstructorInfo
{
    internal CachedConstructorInfo( ICachedType declaringType, ConstructorInfo ctor )
        : base( declaringType, ctor )
    {
    }

    public ConstructorInfo ConstructorInfo => Unsafe.As<ConstructorInfo>( _member );

    public override StringBuilder Write( StringBuilder b )
    {
        b.Append( DeclaringType.Name ).Append( '(' );
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
