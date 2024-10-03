using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// Unifies <see cref="IStObjFinalImplementation"/> and <see cref="IStObjServiceClassDescriptor"/>.
/// </summary>
public interface IStObjFinalClass 
{
    /// <summary>
    /// Gets the class type of the most specialized type.
    /// </summary>
    Type ClassType { get; }

    /// <summary>
    /// Gets the actual Type that must be instantiated. It is <see cref="ClassType"/> for regular classes but
    /// for abstract classes with Auto implementation, this is the type of the dynamically generated class.
    /// </summary>
    Type FinalType { get; }

    /// <summary>
    /// Gets whether this is a scoped service or a singleton one.
    /// For <see cref="IStObjFinalImplementation"/> this is always false.
    /// </summary>
    bool IsScoped { get; }

    /// <summary>
    /// Gets the interfaces that are marked with <see cref="IsMultipleAttribute"/> and must be mapped to this <see cref="FinalType"/>
    /// regardless of their other mappings.
    /// </summary>
    IReadOnlyCollection<Type> MultipleMappings { get; }

    /// <summary>
    /// Gets the types that are mapped to this <see cref="FinalType"/> and only to it.
    /// </summary>
    IReadOnlyCollection<Type> UniqueMappings { get; }

}
