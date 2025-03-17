using System.Reflection;

namespace CK.Engine.TypeCollector;

abstract class CachedMemberInfo : CachedItem, ICachedMember
{
    readonly ICachedType _declaringType;

    internal CachedMemberInfo( ICachedType declaringType, MemberInfo member )
        : base( member )
    {
        _declaringType = declaringType;
    }

    public ICachedType DeclaringType => _declaringType;

    public MemberInfo Member => _member;

    internal GlobalTypeCache TypeCache => _declaringType.TypeCache;
}
