using System.Reflection;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Cached type member is a <see cref="ICachedEventInfo"/>, <see cref="ICachedFieldInfo"/>,
/// <see cref="ICachedPropertyInfo"/> or a <see cref="ICachedMethodBase"/> that can be a <see cref="ICachedConstructorInfo"/>
/// or a <see cref="ICachedMethodInfo"/>.
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

    /// <summary>
    /// Returns a readable name with the declaring type.
    /// </summary>
    /// <returns>The readable member name.</returns>
    string ToStringWithDeclaringType();

    /// <summary>
    /// Returns a readable name without the declaring type.
    /// </summary>
    /// <returns>The readable member name.</returns>
    string ToString();
}
