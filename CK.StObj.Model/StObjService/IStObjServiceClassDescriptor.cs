using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Describes the final type that must be resolved and whether
    /// it is a scoped or a singleton service.
    /// </summary>
    public interface IStObjServiceClassDescriptor
    {
        /// <summary>
        /// Gets the actual Type that must be instanciated.
        /// This Type has, by design, one and only one public constructor
        /// (see <see cref="StObjServiceClassDescriptorExtension.GetSingleConstructor"/>).
        /// </summary>
        Type ClassType { get; }

        /// <summary>
        /// Gets whether this is a scoped service or a singleton one.
        /// </summary>
        bool IsScoped { get; }

        /// <summary>
        /// Gets whether this is a front only service and if it's the case whether
        /// it is <see cref="FrontServiceKind.IsEndPoint"/> and/or <see cref="FrontServiceKind.IsMarshallable"/>.
        /// </summary>
        FrontServiceKind FrontServiceKind { get; }

        /// <summary>
        /// Gets the types that must be marshalled for this service to be marshallable.
        /// This is empty (if this service is not marshallable), it contains this <see cref="ClassType"/>
        /// (if it is the one that must have a <see cref="StObj.Model.IMarshaller{T}"/> available), or is a set of one or more types
        /// that must have a marshaller.
        /// </summary>
        IReadOnlyCollection<Type> MarshallableFrontServiceTypes { get; }
    }

}
