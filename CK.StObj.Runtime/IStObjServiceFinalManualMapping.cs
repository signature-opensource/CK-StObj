using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Extends base class factory from Model layer with the index
    /// in the list of the simple services mappings.
    /// </summary>
    public interface IStObjServiceFinalManualMapping : IStObjServiceClassFactory
    {
        /// <summary>
        /// Gets the unique index that identifies this class factory: its index
        /// in the <see cref="IStObjServiceMap.ManualMappingList"/>.
        /// </summary>
        int ManualMappingIndex { get; }
    }

}
