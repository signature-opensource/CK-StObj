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
    ICachedMethodInfo Method { get; }

    /// <summary>
    /// Gets whether this method has at least one [ReaDILoop] parameter.
    /// </summary>
    bool IsLoopCallable { get; }

    /// <summary>
    /// Gets whether this method has not been executed.
    /// </summary>
    bool IsWaiting { get; }

    /// <summary>
    /// Gets whether this method is ready to be executed.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets whether this method has been successfully executed.
    /// </summary>
    bool IsCompleted { get; }

    /// <summary>
    /// Gets whether executing this method failed.
    /// When true, this is the culprit that made the engine failed (<see cref="ReaDIEngine.HasError"/>
    /// is necessarily true).
    /// </summary>
    bool IsError { get; }
}
