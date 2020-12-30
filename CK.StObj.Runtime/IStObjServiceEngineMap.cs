using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines an engine extension to the model <see cref="IStObjServiceMap"/>.
    /// This is exposed as the <see cref="IStObjEngineMap.Services"/> property to
    /// mimic <see cref="IStObjMap.Services"/> (and is type compatible).
    /// </summary>
    public interface IStObjServiceEngineMap : IStObjServiceMap
    {
        /// <summary>
        /// Masks the <see cref="IStObjServiceMap.SimpleMappings"/> to expose <see cref="IStObjServiceFinalSimpleMapping"/> (with index)
        /// instead of <see cref="IStObjServiceClassDescriptor"/> from model layer.
        /// </summary>
        new IReadOnlyDictionary<Type, IStObjServiceFinalSimpleMapping> SimpleMappings { get; }

        /// <summary>
        /// Gets all the types without duplicates as <see cref="IStObjServiceFinalSimpleMapping"/>
        /// (with index) instead of <see cref="IStObjServiceClassDescriptor"/> from model layer.
        /// </summary>
        new IReadOnlyList<IStObjServiceFinalSimpleMapping> SimpleMappingList { get; }

        /// <summary>
        /// Masks the <see cref="IStObjServiceMap.ManualMappings"/> to expose <see cref="IStObjServiceFinalManualMapping"/> (with index)
        /// instead of <see cref="IStObjServiceClassFactory"/> from model layer.
        /// </summary>
        new IReadOnlyDictionary<Type, IStObjServiceFinalManualMapping> ManualMappings { get; }

        /// <summary>
        /// Gets all the not so simple registered types without duplicates as <see cref="IStObjServiceFinalManualMapping"/>
        /// (with index) instead of <see cref="IStObjServiceClassFactory"/> from model layer.
        /// See <see cref="ManualMappings"/>.
        /// </summary>
        new IReadOnlyList<IStObjServiceFinalManualMapping> ManualMappingList { get; }
    }
}
