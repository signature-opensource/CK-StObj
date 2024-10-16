using System.Collections.Immutable;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Generalizes <see cref="ICachedMethodInfo"/> and <see cref="ICachedConstructorInfo"/>.
/// </summary>
public interface ICachedMethodBase : ICachedMember
{
    /// <summary>
    /// Gets whether this is a public method.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    ImmutableArray<CachedParameterInfo> ParameterInfos { get; }
}
