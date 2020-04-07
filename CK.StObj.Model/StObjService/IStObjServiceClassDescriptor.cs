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
        /// Gets the actual Type that must be instantiated.
        /// This Type has, by design, one and only one public constructor
        /// (see <see cref="StObjServiceClassDescriptorExtension.GetSingleConstructor"/>).
        /// </summary>
        Type ClassType { get; }

        /// <summary>
        /// Gets the actual Type that must be instantiated. It is <see cref="ClassType"/> for regular classes
        /// but for abstract classes with Auto implementation, this is the type of the dynamically genrerated
        /// class.
        /// </summary>
        Type FinalType { get; }

        /// <summary>
        /// Gets whether this is a scoped service or a singleton one.
        /// </summary>
        bool IsScoped { get; }

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

        /// <summary>
        /// Gets the multiple interfaces that are marked with <see cref="IsMultipleAttribute"/> and that must be mapped to this <see cref="ClassType"/>.
        /// </summary>
        IReadOnlyCollection<Type> MultipleMappings { get; }

        /// <summary>
        /// Gets the types that that must be mapped to this <see cref="ClassType"/> and only to this one.
        /// </summary>
        IReadOnlyCollection<Type> UniqueMappings { get; }

    }

}
