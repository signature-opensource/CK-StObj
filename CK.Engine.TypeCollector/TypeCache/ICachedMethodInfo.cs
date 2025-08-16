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

    /// <summary>
    /// Gets whether this method returns a <see cref="GlobalTypeCache.WellKnownTypes.Task"/>,
    /// <see cref="GlobalTypeCache.WellKnownTypes.GenericTaskDefinition"/>, <see cref="GlobalTypeCache.WellKnownTypes.ValueTask"/>
    /// or a <see cref="GlobalTypeCache.WellKnownTypes.GenericValueTaskDefinition"/>.
    /// </summary>
    bool IsAsynchronous { get; }

    /// <summary>
    /// Unwraps the type T from the Task&lt;T&gt; or ValueTask&lt;T&gt; or returns <see cref="GlobalTypeCache.WellKnownTypes.Void"/>
    /// for non generic Task and ValueTask.
    /// <para>
    /// This is null if this method is not an asynchronous method.
    /// </para>
    /// </summary>
    /// <returns>The unwrapped type or null for a synchronous method.</returns>
    ICachedType? GetAsynchronousReturnedType();
}
