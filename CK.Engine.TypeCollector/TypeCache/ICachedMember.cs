using System.Reflection;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Cached type member.
/// <para>
/// This differs from the .Net reflection model: a <see cref="ICachedType"/> is not a <see cref="ICachedMember"/>:
/// <see cref="ICachedItem"/> generalizes them.
/// </para>
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
