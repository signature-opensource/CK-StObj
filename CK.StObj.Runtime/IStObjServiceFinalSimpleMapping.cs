using CK.Core;

namespace CK.Setup;

/// <summary>
/// Extends base class descriptor from Model layer with the index
/// in the list of the simple services mappings.
/// </summary>
public interface IStObjServiceFinalSimpleMapping : IStObjServiceClassDescriptor
{
    /// <summary>
    /// Gets the unique index that identifies this descriptor: its index in
    /// the <see cref="IStObjServiceMap.MappingList"/>.
    /// </summary>
    int MappingIndex { get; }
}
