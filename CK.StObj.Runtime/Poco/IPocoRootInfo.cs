using System;
using CK.Core;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Defines information for a unified Poco type: this is associated to the
    /// final <see cref="IPocoFactory"/> interface.
    /// </summary>
    public interface IPocoRootInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets the final, unified, type that implements all <see cref="Interfaces"/>.
        /// </summary>
        Type PocoClass { get; }

        /// <summary>
        /// Gets the type that implements the <see cref="IPocoFactory{T}"/> real object for
        /// this Poco type.
        /// </summary>
        Type PocoFactoryClass { get; }

        /// <summary>
        /// Gets the Poco name.
        /// When no [<see cref="ExternalNameAttribute"/>] is defined, this name defaults
        /// to the <see cref="Type.FullName"/> of the <see cref="PrimaryInterface"/>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the command previous names if any.
        /// </summary>
        IReadOnlyList<string> PreviousNames { get; }

        /// <summary>
        /// Gets whether the <see cref="IClosedPoco"/> interface marker appear among the interfaces.
        /// When this is true, then <see cref="ClosureInterface"/> is necessarily not null.
        /// </summary>
        bool IsClosedPoco { get; }

        /// <summary>
        /// Gets the primary interface that defines the Poco: this
        /// is the first entry of the <see cref="Interfaces"/> list.
        /// </summary>
        Type PrimaryInterface => Interfaces[0].PocoInterface;

        /// <summary>
        /// Gets the IPoco interface that "closes" all these <see cref="Interfaces"/>: this interface "unifies"
        /// all the other ones.
        /// If <see cref="IsClosedPoco"/> is true, then this is necessarily not null.
        /// </summary>
        Type? ClosureInterface { get; }

        /// <summary>
        /// Gets all the <see cref="IPocoInterfaceInfo"/> that this Poco implements.
        /// </summary>
        IReadOnlyList<IPocoInterfaceInfo> Interfaces { get; }

        /// <summary>
        /// Gets all the interface types that are not <see cref="IPoco"/> but nevertheless are supported by this <see cref="PocoClass"/>.
        /// See <see cref="IPocoSupportResult.OtherInterfaces"/>.
        /// </summary>
        IReadOnlyCollection<Type> OtherInterfaces { get; }

        /// <summary>
        /// Gets all the properties of this poco indexed by their names.
        /// </summary>
        IReadOnlyDictionary<string, IPocoPropertyInfo> Properties { get; }

        /// <summary>
        /// Gets the properties of this poco.
        /// </summary>
        IReadOnlyList<IPocoPropertyInfo> PropertyList { get; }

        /// <summary>
        /// Gets the properties that are implemented by external code provider.
        /// These properties are typically marked with <see cref="AutoImplementationClaimAttribute"/>.
        /// </summary>
        IReadOnlyList<PropertyInfo> ExternallyImplementedPropertyList { get; }

    }

}
