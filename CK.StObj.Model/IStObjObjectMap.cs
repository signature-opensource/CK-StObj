using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
// Ignore Spelling: Objs

namespace CK.Core;

/// <summary>
/// Fundamental Types to <see cref="IStObj"/> mappings.
/// This is exposed by <see cref="IStObjMap.StObjs"/> and is the result of the setup: its implementation
/// is dynamically generated.
/// </summary>
public interface IStObjObjectMap
{
    /// <summary>
    /// Gets the <see cref="IStObjFinalImplementation"/> or null if no mapping exists.
    /// </summary>
    /// <param name="t">Key type.</param>
    /// <returns>Most specialized StObj or null if no mapping exists for this type.</returns>
    IStObjFinalImplementation? ToLeaf( Type t );

    /// <summary>
    /// Gets the real object final implementation or null if no mapping exists.
    /// </summary>
    /// <param name="t">Key type (that must be a <see cref="IRealObject"/>).</param>
    /// <returns>Structured object instance or null if the type has not been mapped.</returns>
    object? Obtain( Type t );

    /// <summary>
    /// Gets all the real object final implementations that exist in this context.
    /// </summary>
    IReadOnlyList<IStObjFinalImplementation> FinalImplementations { get; }

    /// <summary>
    /// Gets all the <see cref="IStObj"/> and their final implementation that exist in this context.
    /// This contains only classes, not <see cref="IRealObject"/> interfaces. 
    /// </summary>
    IEnumerable<StObjMapping> StObjs { get; }
}
