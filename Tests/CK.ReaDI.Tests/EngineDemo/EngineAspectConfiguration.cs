using CK.Core;

namespace CK.Demo;

public abstract class EngineAspectConfiguration
{
    /// <summary>
    /// Gets the fully qualified name of the type on the Engine side that implements this aspect.
    /// </summary>
    public abstract string AspectType { get; }

}
