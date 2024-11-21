using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;


sealed class CachedFieldInfo : CachedMemberInfo, ICachedFieldInfo
{
    ICachedType? _type;

    internal CachedFieldInfo( ICachedType declaringType, FieldInfo prop )
        : base( declaringType, prop )
    {
    }

    public ICachedType FieldType => _type ??= TypeCache.Get( FieldInfo.FieldType );

    public FieldInfo FieldInfo => Unsafe.As<FieldInfo>( _member );

    public override StringBuilder Write( StringBuilder b )
    {
        FieldType.Write(  b );
        b.Append( ' ' ).Append( Name );
        return b;
    }

    public override string ToString() => Write( new StringBuilder() ).ToString();
}
