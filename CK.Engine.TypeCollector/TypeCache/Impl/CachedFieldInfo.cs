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

    public override StringBuilder Write( StringBuilder b, bool withDeclaringType )
    {
        FieldType.Write(  b );
        b.Append( ' ' );
        if( withDeclaringType ) b.Append( DeclaringType.CSharpName ).Append( '.' );
        b.Append( Name );
        return b;
    }
}
