using System;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an engine extension to the runtime <see cref="IStObjObjectMap"/>.
    /// This is exposed as the <see cref="StObjCollectorResult.StObjs"/> property to
    /// mimic <see cref="IStObjMap.StObjs"/> (and is type compatible).
    /// </summary>
    public interface IStObjObjectEngineMap : IStObjObjectMap
    {
        /// <summary>
        /// Gets the most specialized <see cref="IStObjResult"/> or null if no mapping exists.
        /// </summary>
        /// <param name="t">Key type.</param>
        /// <returns>Most specialized StObj or null if no mapping exists for this type.</returns>
        new IStObjResult ToLeaf( Type t );

        /// <summary>
        /// Gets the most abstract type for any type mapped.
        /// </summary>
        /// <param name="t">Any mapped type.</param>
        /// <returns>The most abstract, less specialized, associated type.</returns>
        IStObjResult ToStObj( Type t );

    }
}
