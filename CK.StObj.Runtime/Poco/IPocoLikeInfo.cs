using System;
using CK.Core;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Defines information for a Poco-like class.
    /// A Poco-like class is aimed to be exchanged, serialized and deserialized just like IPoco.
    /// <list type="bullet">
    ///  <item>It must be a public concrete class.
    ///  <item>It must have a default public constructor.</item>
    ///  <item>It can expose public read only non null IList, IDictionary, ISet (initialized in the constructor).</item>
    /// </list>
    /// <para>
    /// Exposing read only IPoco are not currently supported. This would mean that the constructor takes
    /// the IPoco instances and/or the IPocoFactory for them.
    /// </para>
    /// </summary>
    public interface IPocoLikeInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets the type of this Poco-like (necessarily concrete) class.
        /// </summary>
        Type PocoClass { get; }

        /// <summary>
        /// Gets the Poco-like name.
        /// When no [<see cref="ExternalNameAttribute"/>] is defined, this name defaults
        /// to the <see cref="Type.FullName"/> of the <see cref="PocoClass"/>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the command previous names if any.
        /// </summary>
        IReadOnlyList<string> PreviousNames { get; }

        /// <summary>
        /// Gets whether <see cref="Specializations"/> is not empty.
        /// </summary>
        bool HasSpecializations => Specializations.Count > 0;

        /// <summary>
        /// Gets the specializations of this <see cref="PocoClass"/> that appear in the transitive closure of
        /// all the IPoco.
        /// </summary>
        IReadOnlyList<IPocoLikeInfo> Specializations { get; }

        /// <summary>
        /// Gets all the properties of this poco indexed by their names.
        /// </summary>
        IReadOnlyDictionary<string, IPocoLikePropertyInfo> Properties { get; }

        /// <summary>
        /// Gets the properties of this poco.
        /// </summary>
        IReadOnlyList<IPocoLikePropertyInfo> PropertyList { get; }

    }

}
