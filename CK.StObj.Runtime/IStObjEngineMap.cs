using System;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an engine extension to the runtime <see cref="IStObjObjectMap"/>.
    /// This is exposed as the <see cref="StObjCollectorResult.StObjs"/> property to
    /// mimic <see cref="IStObjMap.StObjs"/> (and is type compatible).
    /// </summary>
    public interface IStObjEngineMap : IStObjMap
    {
        /// <summary>
        /// Gets the <see cref="IStObjFinalClass"/> from any type that can be uniquely
        /// associated to a final implemetation.
        /// </summary>
        /// <param name="t">The type.</param>
        /// <returns>The final implementation or null if no such final implementation exists for the type.</returns>
        IStObjFinalClass Find( Type t );

        /// <summary>
        /// Gets the engine extended StObjs map.
        /// </summary>
        new IStObjObjectEngineMap StObjs { get; }

        /// <summary>
        /// Gets the engine extended Service map.
        /// </summary>
        new IStObjServiceEngineMap Services { get; }

    }
}
