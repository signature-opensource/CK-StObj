using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;


public sealed class CachedField : CachedMember
{
    ICachedType? _type;

    internal CachedField( ICachedType declaringType, FieldInfo prop )
        : base( declaringType, prop )
    {
    }

    public ICachedType FieldType => _type ??= TypeCache.Get( FieldInfo.FieldType );

    public FieldInfo FieldInfo => Unsafe.As<FieldInfo>( _member );

    internal override StringBuilder Write( StringBuilder b, bool withDeclaringType )
    {
        FieldType.Write(  b );
        b.Append( ' ' );
        if( withDeclaringType ) b.Append( DeclaringType.CSharpName ).Append( '.' );
        b.Append( Name );
        return b;
    }
}
