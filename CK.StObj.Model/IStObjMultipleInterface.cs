using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Final information for IEnumerable&lt;T&gt; where T is an interface marked with [IsMultiple] attribute.
    /// </summary>
    public interface IStObjMultipleInterface
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
        /// Gets the final real objects or auto services that this enumeration contains.
        /// </summary>
        IReadOnlyCollection<IStObjFinalClass> Implementations { get; }

        /// <summary>
        /// Gets the types that must be marshalled for this enumeration to be marshallable.
        /// This is empty if this service is not marshallable.
        /// <para>
        /// Note that it contains this <see cref="EnumerableType"/> if the enumerated interface itself is marked with [IsMarshallable]
        /// and is the one that must have a <see cref="StObj.Model.IMarshaller{T}"/> available and contains the <see cref="ItemType"/>
        /// if it is the IsMultiple interface that is declared marshallable. Both of these are strange, but who knows...
        /// </para>
        /// </summary>
        IReadOnlyCollection<Type> MarshallableTypes { get; }

    }
}
