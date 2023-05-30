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
        /// Masks the <see cref="IStObjServiceMap.Mappings"/> to expose <see cref="IStObjServiceFinalSimpleMapping"/> (with index)
        /// instead of <see cref="IStObjServiceClassDescriptor"/> from model layer.
        /// </summary>
        new IReadOnlyDictionary<Type, IStObjServiceFinalSimpleMapping> Mappings { get; }

        /// <summary>
        /// Gets all the types without duplicates as <see cref="IStObjServiceFinalSimpleMapping"/>
        /// (with index) instead of <see cref="IStObjServiceClassDescriptor"/> from model layer.
        /// </summary>
        new IReadOnlyList<IStObjServiceFinalSimpleMapping> MappingList { get; }
    }
}
