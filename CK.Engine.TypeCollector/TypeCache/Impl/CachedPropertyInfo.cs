using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;

public interface ICachedPropertyInfo : ICachedMember
{
    ICachedType PropertyType { get; }
}


sealed class CachedPropertyInfo : CachedMemberInfo, ICachedPropertyInfo
{
    ICachedType? _type;

    internal CachedPropertyInfo( ICachedType declaringType, PropertyInfo prop )
        : base( declaringType, prop )
    {
    }

    public ICachedType PropertyType => _type ??= TypeCache.Get( PropertyInfo.PropertyType );

    public PropertyInfo PropertyInfo => Unsafe.As<PropertyInfo>( _member );

    public override StringBuilder Write( StringBuilder b )
    {
        PropertyType.Write(  b );
        b.Append( ' ' ).Append( Name );
        return b;
    }

    public override string ToString() => Write( new StringBuilder() ).ToString();
}
