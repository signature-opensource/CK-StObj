using System.Reflection;

namespace CK.Engine.TypeCollector;

public interface ICachedMethodInfo : ICachedMethodBase
{
    /// <summary>
    /// Gets whether this is a static method.
    /// </summary>
    bool IsStatic { get; }

    /// <summary>
    /// Gets the cached info. Should rarely be used directly.
    /// </summary>
    MethodInfo MethodInfo { get; }

    /// <summary>
    /// Gets the return parameter.
    /// </summary>
    CachedParameterInfo ReturnParameterInfo { get; }
}
