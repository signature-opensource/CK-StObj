using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;


sealed class CachedPropertyInfo : CachedMemberInfo, ICachedPropertyInfo
{
    ICachedType? _type;

    internal CachedPropertyInfo( ICachedType declaringType, PropertyInfo prop )
        : base( declaringType, prop )
    {
    }

    public ICachedType PropertyType => _type ??= TypeCache.Get( PropertyInfo.PropertyType );

    public PropertyInfo PropertyInfo => Unsafe.As<PropertyInfo>( _member );

    public override StringBuilder Write( StringBuilder b, bool withDeclaringType )
    {
        PropertyType.Write( b );
        b.Append( ' ' );
        if( withDeclaringType ) b.Append( DeclaringType.CSharpName ).Append( '.' );
        b.Append( Name );
        return b;
    }
}
