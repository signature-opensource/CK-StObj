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
        Type ItemType { get; }

        /// <summary>
        /// Gets the <see cref="IEnumerable{T}"/> of <see cref="ItemType"/> type.
        /// </summary>
        Type EnumerableType { get; }

        /// <summary>
        /// Gets the types that must be marshalled for this enumeration to be marshallable.
        /// This is empty if this service is not marshallable.
        /// <para>
        /// Note that it contains this <see cref="ItemType"/> if the enumerated interface itself is marked with [IsMarshallable]
        /// and is the one that must have a <see cref="StObj.Model.IMarshaller{T}"/> available.
        /// </para>
        /// </summary>
        IReadOnlyCollection<Type> MarshallableTypes { get; }
    }
}
