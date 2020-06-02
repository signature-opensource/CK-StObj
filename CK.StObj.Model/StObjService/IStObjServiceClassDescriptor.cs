using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Describes the final type that must be resolved and whether
    /// it is a scoped or a singleton service.
    /// </summary>
    public interface IStObjServiceClassDescriptor : IStObjFinalClass
    {
        /// <summary>
        /// Gets the service kind.
        /// </summary>
        AutoServiceKind AutoServiceKind { get; }

        /// <summary>
        /// Gets the types that must be marshalled for this service to be marshallable.
        /// This is empty (if this service is not marshallable), it contains this <see cref="ClassType"/>
        /// (if it is the one that must have a <see cref="StObj.Model.IMarshaller{T}"/> available), or is a set of one or more types
        /// that must have a marshaller.
        /// </summary>
        IReadOnlyCollection<Type> MarshallableTypes { get; }

    }

}
