using System;
using CK.Core;

namespace CK.Setup;

/// <summary>
/// A locator combines a <see cref="Type"/> and a path to a resource. 
/// </summary>
/// <remarks>
/// The path may begin with a ~ and in such case, the resource path is "assembly based"
/// and the <see cref="Type"/> is used only for its assembly.
/// </remarks>
public interface IResourceLocator
{
    /// <summary>
    /// Gets the type that will be used to locate the resource: its <see cref="Type.Namespace"/> is the path prefix of the resources.
    /// The resources must belong to its <see cref="System.Reflection.Assembly"/>.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets a sub path from the namespace of the <see cref="Type"/> to the resources.
    /// Can be null or <see cref="String.Empty"/> if the resources are directly 
    /// associated to the type.
    /// </summary>
    string? Path { get; }
}
