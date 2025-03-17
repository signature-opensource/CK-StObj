using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// Final information for IEnumerable&lt;T&gt; where T is an interface marked with [IsMultiple] attribute.
/// </summary>
public interface IStObjMultipleInterface
{
    /// <summary>
    /// Gets whether this enumeration must be scoped or can be registered as a singleton.
    /// </summary>
    bool IsScoped { get; }

    /// <summary>
    /// Gets the enumerated interface type.
    /// </summary>
    Type ItemType { get; }

    /// <summary>
    /// Gets the <see cref="IEnumerable{T}"/> of <see cref="ItemType"/> type.
    /// </summary>
    Type EnumerableType { get; }

    /// <summary>
    /// Gets the final real objects or auto services that this enumeration contains.
    /// </summary>
    IReadOnlyCollection<IStObjFinalClass> Implementations { get; }

}
