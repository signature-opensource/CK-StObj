using CK.Engine.TypeCollector;

namespace CK.Core;

/// <summary>
/// The [ReaDI] method of a <see cref="IReaDIHandler"/>.
/// </summary>
public interface IReaDIMethod
{
    /// <summary>
    /// The handler type.
    /// </summary>
    ICachedType Handler { get; }

    /// <summary>
    /// The [ReaDI] method.
    /// </summary>
    CachedMethod Method { get; }
}
