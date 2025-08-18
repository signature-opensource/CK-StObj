using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

abstract class CachedMemberInfo : CachedItem, ICachedMember
{
    readonly ICachedType _declaringType;
    string? _toString;
    string? _toStringWithDeclaringType;

    internal CachedMemberInfo( ICachedType declaringType, MemberInfo member )
        : base( member )
    {
        _declaringType = declaringType;
    }

    public ICachedType DeclaringType => _declaringType;

    public MemberInfo Member => _member;

    public override sealed StringBuilder Write( StringBuilder b ) => Write( b, false );

    public abstract StringBuilder Write( StringBuilder b, bool withDeclaringType );

    public override sealed GlobalTypeCache TypeCache => _declaringType.TypeCache;

    public string ToStringWithDeclaringType() => _toStringWithDeclaringType ??= Write( new StringBuilder(), true ).ToString();

    public override sealed string ToString() => _toString ??= Write( new StringBuilder(), false ).ToString();
}
