using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an engine extension to the runtime <see cref="IStObjMap"/>.
    /// </summary>
    public interface IStObjEngineMap : IStObjMap
    {
        /// <summary>
        /// Gets the engine extended StObjs map.
        /// </summary>
        new IStObjObjectEngineMap StObjs { get; }

        /// <summary>
        /// Gets the engine extended Service map.
        /// </summary>
        new IStObjServiceEngineMap Services { get; }

        /// <summary>
        /// Gets the endpoints informations.
        /// </summary>
        IDIContainerAnalysisResult EndpointResult { get; }

        /// <summary>
        /// Gets all the type's <see cref="ITypeAttributesCache"/>.
        /// </summary>
        IReadOnlyDictionary<Type, ITypeAttributesCache> AllTypesAttributesCache { get; }

    }
}
