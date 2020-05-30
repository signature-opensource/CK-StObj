using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Extends base class descriptor from Model layer with the index
    /// in the list of the simple services mappings.
    /// </summary>
    public interface IStObjServiceFinalSimpleMapping : IStObjServiceClassDescriptor
    {
        /// <summary>
        /// Gets the unique index that identifies this descriptor: its index in
        /// the <see cref="IStObjServiceMap.SimpleMappingList"/>.
        /// </summary>
        int SimpleMappingIndex { get; }
    }

}
