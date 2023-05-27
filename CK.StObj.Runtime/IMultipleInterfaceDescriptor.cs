using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Final information for IEnumerable&lt;T&gt; where T is an interface marked with [IsMultiple] attribute.
    /// This is currently not exposed in the <see cref="IStObjMap"/>, it is exposed by the <see cref="IStObjEngineMap.MultipleMappings"/>.
    /// </summary>
    public interface IMultipleInterfaceDescriptor
    {
        /// <summary>
        /// Gets whether this enumeration must be scoped or can be registered as a singleton.
        /// </summary>
        bool IsScoped { get; }

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

        /// <summary>
        /// Gets the final real objects or auto services that this enumeration contains. 
        /// </summary>
        IEnumerable<IStObjFinalClass> Implementations { get; }

        /// <summary>
        /// Gets the count of <see cref="Implementations"/>.
        /// </summary>
        int ImplementationCount { get; }
    }
}
