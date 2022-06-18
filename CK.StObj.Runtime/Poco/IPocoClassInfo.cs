using System;
using CK.Core;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Defines information for a Poco-like object: a class that has the <see cref="PocoClassAttribute"/> and appears as a
    /// property's direct type (or as a typed parameter of standard collections - see <see cref="IPocoPropertyInfo.IsStandardCollectionType"/> of 
    /// of a IPoco or another Poco-like object (recursively) is a Poco-like object.
    /// A Poco-like object must behave like a IPoco.
    /// <para>
    /// Only "standard collections" should be used: a Poco-like object must expose public read only non null HashSet&lt;&gt;,
    /// List&lt;&gt;, or Dictionary&lt;,&gt; (that must be initialized in the constructor) or
    /// mutable (possibly nullable) properties of these types (or simple arrays).
    /// </para>
    /// </summary>
    public interface IPocoClassInfo : IAnnotationSet
    {
        /// <summary>
        /// Gets the type of this Poco-like class.
        /// </summary>
        Type PocoType { get; }

        /// <summary>
        /// Gets the Poco-like name.
        /// When no [<see cref="ExternalNameAttribute"/>] is defined, this name defaults
        /// to the <see cref="Type.FullName"/> of the <see cref="PocoType"/>.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the previous names of this Poco like object if any.
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
        IReadOnlyList<IPocoClassInfo> Specializations { get; }

        /// <summary>
        /// Gets all the properties of this poco indexed by their names.
        /// </summary>
        IReadOnlyDictionary<string, IPocoClassPropertyInfo> Properties { get; }

        /// <summary>
        /// Gets the properties of this poco.
        /// </summary>
        IReadOnlyList<IPocoClassPropertyInfo> PropertyList { get; }

    }

}
