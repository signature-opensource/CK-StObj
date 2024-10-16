using System.Reflection;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Cached type member.
/// This differs from the .Net model: a <see cref="ICachedType"/> is not a <see cref="ICachedMember"/>.
/// </summary>
public interface ICachedMember : ICachedItem
{
    /// <summary>
    /// Gets the type that declares this member.
    /// </summary>
    ICachedType DeclaringType { get; }

    /// <summary>
    /// Gets the cached <see cref="MemberInfo"/>.
    /// </summary>
    MemberInfo Member { get; }
}
