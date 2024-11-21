using System;

namespace CK.Core;

/// <summary>
/// Base interface that describes an "object slice".
/// </summary>
public interface IStObj
{
    /// <summary>
    /// Gets the class type of this "slice" of the object.
    /// </summary>
    Type ClassType { get; }

    /// <summary>
    /// Gets the parent <see cref="IStObj"/> in the inheritance chain (the one associated to the base class of this <see cref="ClassType"/>).
    /// </summary>
    IStObj? Generalization { get; }

    /// <summary>
    /// Gets the child <see cref="IStObj"/> in the inheritance chain.
    /// Null when this is the <see cref="FinalImplementation"/>.
    /// </summary>
    IStObj? Specialization { get; }

    /// <summary>
    /// Gets the final implementation (the most specialized type).
    /// </summary>
    IStObjFinalImplementation FinalImplementation { get; }

    /// <summary>
    /// Gets the index of this IStObj in the whole ordered list of StObj.
    /// </summary>
    int IndexOrdered { get; }
}
