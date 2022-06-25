using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Final information for IEnumerable&lt;T&gt; where T is an interface marked with [IsMultiple] attribute.
    /// This is currently not exposed in the <see cref="IStObjObjectEngineMap"/> or <see cref="CK.Core.IStObjMap"/>
    /// but it should be (at least on the EngineMap) to support code generation for marshallable services.
    /// </summary>
    public interface IMultipleInterfaceDescriptor
    {
        /// <summary>
        /// Gets the enumerated interface type.
        /// </summary>
        Type EnumeratedType { get; }

        /// <summary>
        /// Gets the <see cref="IEnumerable{T}"/> of <see cref="EnumeratedType"/> type.
        /// </summary>
        Type EnumerableType { get; }

        /// <summary>
        /// Gets the types that must be marshalled for this enumeration to be marshallable.
        /// This is null until the <see cref="AutoServiceClassInfo.ComputeFinalTypeKind"/> method has been called.
        /// This is empty (if this service is not marshallable), it contains this <see cref="EnumeratedType"/>
        /// (if the enumerated interface is marked with [IsMarshallable] and is the one that must have a <see cref="StObj.Model.IMarshaller{T}"/>
        /// available), or is a set of one or more types that must have a marshaller.
        /// </summary>
        IReadOnlyCollection<Type> MarshallableInProcessTypes { get; }

        /// <summary>
        /// Gets the types that must be marshalled for this enumeration to be marshallable inside the same process.
        /// This is empty if this service is not marshallable or if it doesn't need to be: only services marked
        /// with <see cref="CKTypeKind.IsFrontService"/> are concerned since, by design, services that are
        /// not front services at all or are <see cref="CKTypeKind.IsFrontProcessService"/> don't need to be
        /// marshalled inside the same process.
        /// <para>
        /// Note that it contains this <see cref="EnumeratedType"/> if the enumerated interface is marked with [IsMarshallable] and is the one
        /// that must have a <see cref="StObj.Model.IMarshaller{T}"/> available.
        /// </para>
        /// </summary>
        IReadOnlyCollection<Type> MarshallableTypes { get; }
    }
}
