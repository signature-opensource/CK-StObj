using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an engine extension to the model <see cref="IStObjObjectMap"/>.
    /// This is exposed as the <see cref="IStObjEngineMap.StObjs"/> property to
    /// mimic <see cref="IStObjMap.StObjs"/> (and is type compatible).
    /// </summary>
    public interface IStObjObjectEngineMap : IStObjObjectMap
    {
        /// <summary>
        /// Gets the most specialized <see cref="IStObjResult"/> or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type.</param>
        /// <returns>Most specialized StObj or null if no mapping exists for this type.</returns>
        new IStObjFinalImplementationResult? ToLeaf( Type t );

        /// <summary>
        /// Gets the most abstract type for any type mapped.
        /// </summary>
        /// <param name="t">Any mapped type.</param>
        /// <returns>The most abstract, less specialized, associated type.</returns>
        IStObjResult? ToHead( Type t );

        /// <summary>
        /// Gets all the <see cref="IStObjResult"/> ordered by their dependencies.
        /// </summary>
        IReadOnlyList<IStObjResult> OrderedStObjs { get; }

        /// <summary>
        /// Gets the final, most specialized, <see cref="IStObjFinalImplementationResult"/>.
        /// </summary>
        new IReadOnlyCollection<IStObjFinalImplementationResult> FinalImplementations { get; }
    }
}
