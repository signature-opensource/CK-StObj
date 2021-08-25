using System;
using CK.Core;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Defines information for a Poco-like object: a struct or a class that appears as a property
    /// of a IPoco or another Poco-like object (recursively) is a Poco-like object.
    /// A Poco-like object is aimed to be exchanged, serialized and deserialized just like IPoco.
    /// <para>
    /// If and how these objects are serializable/exportable/marshallable is not our concern here: this
    /// is up to the serializer/exporter/marshaller technology. This IPocoLikeInfo and its <see cref="Properties"/>
    /// is more a centralized cache of information (thanks to the <see cref="IAnnotationSet"/> support) that
    /// captures once for all the transitive closure of the IPoco.
    /// </para>
    /// <para>
    /// Considering collections, a Poco-like object should expose public read only non null ISet&lt;&gt;, Set&lt;&gt;,
    /// IList&lt;&gt;, List&lt;&gt;, IDictionary&lt;,&gt; or Dictionary&lt;,&gt; (that must be initialized in the constructor) or
    /// mutable properties of these types that should be handled by all serializer/exporter/marshaller.
    /// </para>
    /// </summary>
    public interface IPocoLikeInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets the type of this Poco-like (necessarily concrete) class.
        /// </summary>
        Type PocoType { get; }

        /// <summary>
        /// Gets the Poco-like name.
        /// When no [<see cref="ExternalNameAttribute"/>] is defined, this name defaults
        /// to the <see cref="Type.FullName"/> of the <see cref="PocoType"/>.
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
        /// Gets the specializations of this <see cref="PocoType"/> that appear in the transitive closure of
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
